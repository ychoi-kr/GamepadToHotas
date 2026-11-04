using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace GamepadToHotas
{
    public class MainForm : Form
    {
        private readonly string _profilesDir;
        private List<ControllerProfile> _profiles = new List<ControllerProfile>();
        private ControllerProfile _currentProfile;

        private readonly XInputProvider _xinputProvider = new XInputProvider();
        private readonly WinMMInputProvider _winmmProvider = new WinMMInputProvider();
        private readonly VJoyOutput _vjoy = new VJoyOutput();
        private readonly MappingEngine _engine = new MappingEngine();

        private NormalizedControllerState _latestInput = new NormalizedControllerState();
        private VJoyStateSnapshot _latestOutput = new VJoyStateSnapshot();

        private Thread _workerThread;
        private volatile bool _running = true;

        // UI Controls
        private ComboBox _cboProfiles;
        private Label _lblInputStatus;
        private Label _lblVJoyStatus;
        private Button _btnAutoConfigureVJoy;
        private Button _btnTogglePause;
        private Label _lblPulseBanner;
        private Panel _pnlAxesList;
        private FlowLayoutPanel _pnlButtonsGrid;
        private System.Windows.Forms.Timer _uiTimer;

        // Dynamic Axis UI rows
        private class AxisUiRow
        {
            public AxisRule Rule;
            public Label TitleLabel;
            public InstantAxisBar Bar;
            public Label ValueLabel;
            public CheckBox InvertCheck;
            public Button SoloBindMinusButton;
            public Button SoloBindPlusButton;
            public int LastPct = -1;
        }

        /// <summary>
        /// Custom lightweight axis bar without the Win32 ProgressBar forward-fill animation delay.
        /// Repaints only when the value actually changes.
        /// </summary>
        private sealed class InstantAxisBar : Control
        {
            private static readonly SolidBrush BgBrush = new SolidBrush(Color.FromArgb(22, 25, 32));
            private static readonly SolidBrush FillBrush = new SolidBrush(Color.FromArgb(46, 184, 92));
            private static readonly Pen CenterTickPen = new Pen(Color.FromArgb(95, 110, 135), 1f);
            private static readonly Pen BorderPen = new Pen(Color.FromArgb(70, 80, 100), 1f);

            private int _minimum = VJoyOutput.AXIS_MIN;
            private int _maximum = VJoyOutput.AXIS_MAX;
            private int _value = VJoyOutput.AXIS_MID;

            public int Minimum
            {
                get { return _minimum; }
                set { _minimum = value; Invalidate(); }
            }

            public int Maximum
            {
                get { return _maximum; }
                set { _maximum = value; Invalidate(); }
            }

            public int Value
            {
                get { return _value; }
                set
                {
                    int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                    if (_value != clamped)
                    {
                        _value = clamped;
                        Invalidate();
                    }
                }
            }

            public InstantAxisBar()
            {
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.Opaque,
                    true);
                TabStop = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width;
                int h = Height;

                g.FillRectangle(BgBrush, 0, 0, w, h);

                int range = _maximum - _minimum;
                if (range > 0 && w > 2 && h > 2)
                {
                    int innerMaxW = w - 2;
                    int fillW = (int)Math.Round(((double)(_value - _minimum) / range) * innerMaxW);
                    if (fillW > 0)
                    {
                        if (fillW > innerMaxW) fillW = innerMaxW;
                        g.FillRectangle(FillBrush, 1, 1, fillW, h - 2);
                    }

                    int midX = 1 + (innerMaxW / 2);
                    g.DrawLine(CenterTickPen, midX, 1, midX, h - 2);
                }

                g.DrawRectangle(BorderPen, 0, 0, w - 1, h - 1);
            }
        }

        private class ButtonUiCell
        {
            public ButtonRule Rule;
            public Button TriggerButton;
        }

        private readonly List<AxisUiRow> _axisRows = new List<AxisUiRow>();
        private readonly List<ButtonUiCell> _buttonCells = new List<ButtonUiCell>();

        public MainForm()
        {
            Text = "GamepadToHOTAS - Mode 2 Virtual HOTAS Bridge (vJoy)";
            Size = new Size(880, 720);
            MinimumSize = new Size(840, 660);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 26, 32);
            ForeColor = Color.FromArgb(230, 234, 240);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _profilesDir = Path.Combine(baseDir, "profiles");
            if (!Directory.Exists(_profilesDir))
            {
                Directory.CreateDirectory(_profilesDir);
            }

            BuildUi();
            LoadProfiles();
            StartWorkerThread();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30 FPS UI refresh
            _uiTimer.Tick += OnUiTimerTick;
            _uiTimer.Start();
        }

        private void BuildUi()
        {
            // 1. Top Profile & Status Header
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 128,
                Padding = new Padding(14),
                BackColor = Color.FromArgb(32, 35, 44)
            };

            var lblProfileTitle = new Label
            {
                Text = "Profile (profiles/*.json):",
                Location = new Point(14, 15),
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 175, 195)
            };

            _cboProfiles = new ComboBox
            {
                Location = new Point(175, 11),
                Width = 410,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _cboProfiles.SelectedIndexChanged += (s, e) =>
            {
                if (_cboProfiles.SelectedIndex >= 0 && _cboProfiles.SelectedIndex < _profiles.Count)
                {
                    SelectProfile(_profiles[_cboProfiles.SelectedIndex]);
                }
            };

            var btnReload = CreateStyledButton("Reload JSON", new Point(595, 10), new Size(105, 28), Color.FromArgb(55, 62, 78));
            btnReload.Click += (s, e) => LoadProfiles();

            var btnOpenDir = CreateStyledButton("Open Folder", new Point(708, 10), new Size(105, 28), Color.FromArgb(55, 62, 78));
            btnOpenDir.Click += (s, e) =>
            {
                if (Directory.Exists(_profilesDir))
                    Process.Start("explorer.exe", _profilesDir);
            };

            _lblInputStatus = new Label
            {
                Text = "Gamepad: Scanning...",
                Location = new Point(14, 50),
                Size = new Size(400, 24),
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 210, 150)
            };

            _btnTogglePause = CreateStyledButton(" Bridge Active (Back+Start to Pause)", new Point(425, 46), new Size(388, 28), Color.FromArgb(42, 95, 68));
            _btnTogglePause.Click += (s, e) =>
            {
                _engine.IsPaused = !_engine.IsPaused;
                UpdateStatusIndicators();
            };

            _lblVJoyStatus = new Label
            {
                Text = "vJoy Status: Checking...",
                Location = new Point(14, 86),
                Size = new Size(560, 24),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 210, 225)
            };

            _btnAutoConfigureVJoy = CreateStyledButton("Auto-Configure vJoy #1 (16 Btn + POV + 8 Axes)", new Point(580, 82), new Size(233, 30), Color.FromArgb(180, 95, 35));
            _btnAutoConfigureVJoy.Click += OnAutoConfigureVJoyClick;

            pnlHeader.Controls.AddRange(new Control[]
            {
                lblProfileTitle, _cboProfiles, btnReload, btnOpenDir,
                _lblInputStatus, _btnTogglePause, _lblVJoyStatus, _btnAutoConfigureVJoy
            });

            // 2. Solo Binding Helper Banner
            _lblPulseBanner = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "💡 Tip: Click [Bind -] (Left/Down) or [Bind +] (Right/Up) below, then click the slot in Wardogs. (Click this banner anytime to stop pulsing)",
                BackColor = Color.FromArgb(40, 48, 66),
                ForeColor = Color.FromArgb(215, 228, 250),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _lblPulseBanner.Click += (s, e) =>
            {
                if (_engine.HasActivePulse)
                {
                    _engine.CancelBindPulse();
                }
            };

            // 3. Main Content Container
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                AutoScroll = true
            };

            var lblAxesHeader = new Label
            {
                Text = "MODE 2 AXES MAPPING (Real-time Output & Solo Bind Helper)",
                Location = new Point(14, 10),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 185, 255)
            };

            _pnlAxesList = new Panel
            {
                Location = new Point(14, 36),
                Size = new Size(825, 250),
                BackColor = Color.FromArgb(30, 33, 42),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblButtonsHeader = new Label
            {
                Text = "HOTAS BUTTONS & POV HAT (Click any button below to trigger a 3-second Solo Bind Pulse)",
                Location = new Point(14, 298),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 185, 255)
            };

            _pnlButtonsGrid = new FlowLayoutPanel
            {
                Location = new Point(14, 324),
                Size = new Size(825, 185),
                BackColor = Color.FromArgb(30, 33, 42),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),
                AutoScroll = true
            };

            pnlMain.Controls.AddRange(new Control[]
            {
                lblAxesHeader, _pnlAxesList,
                lblButtonsHeader, _pnlButtonsGrid
            });

            Controls.Add(pnlMain);
            Controls.Add(_lblPulseBanner);
            Controls.Add(pnlHeader);
        }

        private Button CreateStyledButton(string text, Point location, Size size, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 90, 110);
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private void LoadProfiles()
        {
            _profiles.Clear();
            _cboProfiles.Items.Clear();

            if (Directory.Exists(_profilesDir))
            {
                string[] files = Directory.GetFiles(_profilesDir, "*.json");
                if (files.Length == 0)
                {
                    try
                    {
                        string defaultPath = Path.Combine(_profilesDir, "gamesir_t3_lite_mode2.json");
                        var defProfile = ControllerProfile.CreateDefaultMode2Profile();
                        defProfile.SaveToFile(defaultPath);
                        files = new string[] { defaultPath };
                    }
                    catch { }
                }

                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string f in files)
                {
                    try
                    {
                        var p = ControllerProfile.LoadFromFile(f);
                        _profiles.Add(p);
                        _cboProfiles.Items.Add(string.Format("{0}  ({1})", p.displayName ?? p.profileId, Path.GetFileName(f)));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to load profile " + Path.GetFileName(f) + ":\n" + ex.Message,
                            "Profile Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }

            if (_profiles.Count > 0)
            {
                _cboProfiles.SelectedIndex = 0;
            }
        }

        private void SelectProfile(ControllerProfile profile)
        {
            _currentProfile = profile;
            if (profile != null && profile.vjoyTarget != null)
            {
                _vjoy.Acquire(profile.vjoyTarget.deviceId);
            }
            RebuildProfileUi();
            UpdateStatusIndicators();
        }

        private void RebuildProfileUi()
        {
            _pnlAxesList.Controls.Clear();
            _axisRows.Clear();
            _pnlButtonsGrid.Controls.Clear();
            _buttonCells.Clear();

            if (_currentProfile == null) return;

            int y = 10;
            if (_currentProfile.axes != null)
            {
                foreach (var axRule in _currentProfile.axes)
                {
                    var currentRule = axRule;
                    var lbl = new Label
                    {
                        Text = string.Format("{0}\n({1} ➔ vJoy {2})", currentRule.name, currentRule.source, currentRule.target),
                        Location = new Point(12, y),
                        Size = new Size(245, 34),
                        ForeColor = Color.FromArgb(220, 225, 235)
                    };

                    var bar = new InstantAxisBar
                    {
                        Location = new Point(260, y + 6),
                        Size = new Size(215, 20),
                        Minimum = VJoyOutput.AXIS_MIN,
                        Maximum = VJoyOutput.AXIS_MAX,
                        Value = VJoyOutput.AXIS_MID
                    };

                    var valLbl = new Label
                    {
                        Text = "50%",
                        Location = new Point(480, y + 6),
                        Size = new Size(52, 22),
                        TextAlign = ContentAlignment.MiddleRight,
                        ForeColor = Color.FromArgb(170, 200, 240)
                    };

                    var chkInvert = new CheckBox
                    {
                        Text = "Invert",
                        Checked = currentRule.invert,
                        Location = new Point(540, y + 6),
                        Size = new Size(65, 22),
                        ForeColor = Color.FromArgb(200, 205, 215)
                    };
                    chkInvert.CheckedChanged += (s, e) =>
                    {
                        currentRule.invert = chkInvert.Checked;
                        try { _currentProfile.SaveToFile(); } catch { }
                    };

                    var btnMinus = CreateStyledButton("Bind - (Left/Dn)", new Point(610, y + 3), new Size(98, 26), Color.FromArgb(50, 75, 120));
                    btnMinus.Font = new Font("Segoe UI", 8.2f, FontStyle.Regular);
                    btnMinus.Click += (s, e) =>
                    {
                        _engine.ScheduleAxisBindPulse(currentRule.target, currentRule.name, -1, 3.0, 7.5);
                    };

                    var btnPlus = CreateStyledButton("Bind + (Right/Up)", new Point(712, y + 3), new Size(102, 26), Color.FromArgb(45, 95, 115));
                    btnPlus.Font = new Font("Segoe UI", 8.2f, FontStyle.Regular);
                    btnPlus.Click += (s, e) =>
                    {
                        _engine.ScheduleAxisBindPulse(currentRule.target, currentRule.name, 1, 3.0, 7.5);
                    };

                    _pnlAxesList.Controls.AddRange(new Control[] { lbl, bar, valLbl, chkInvert, btnMinus, btnPlus });
                    _axisRows.Add(new AxisUiRow
                    {
                        Rule = currentRule,
                        TitleLabel = lbl,
                        Bar = bar,
                        ValueLabel = valLbl,
                        InvertCheck = chkInvert,
                        SoloBindMinusButton = btnMinus,
                        SoloBindPlusButton = btnPlus
                    });

                    y += 38;
                }
            }

            if (_currentProfile.buttons != null)
            {
                foreach (var bRule in _currentProfile.buttons)
                {
                    var currentBtn = bRule;
                    var btnCtrl = CreateStyledButton(
                        string.Format("Btn #{0}: {1}\n[{2}]", currentBtn.targetButton, currentBtn.label ?? currentBtn.source, currentBtn.source),
                        Point.Empty,
                        new Size(190, 44),
                        Color.FromArgb(44, 48, 60));
                    btnCtrl.Margin = new Padding(4);
                    btnCtrl.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                    btnCtrl.Click += (s, e) =>
                    {
                        _engine.ScheduleButtonBindPulse(currentBtn.targetButton, currentBtn.label ?? currentBtn.source, 3.0, 6.0);
                    };

                    _pnlButtonsGrid.Controls.Add(btnCtrl);
                    _buttonCells.Add(new ButtonUiCell
                    {
                        Rule = currentBtn,
                        TriggerButton = btnCtrl
                    });
                }
            }

            // Also add POV Bind Helpers if POV is configured
            if (_currentProfile.povs != null && _currentProfile.povs.Count > 0)
            {
                var pov = _currentProfile.povs[0];
                string[] dirs = { "Up", "Right", "Down", "Left" };
                int[] angles = { 0, 9000, 18000, 27000 };
                for (int i = 0; i < 4; i++)
                {
                    int idx = i;
                    var povBtn = CreateStyledButton(
                        string.Format("POV #{0} {1}\n(Solo Bind)", pov.targetPov, dirs[idx]),
                        Point.Empty,
                        new Size(190, 44),
                        Color.FromArgb(52, 48, 72));
                    povBtn.Margin = new Padding(4);
                    povBtn.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                    povBtn.Click += (s, e) =>
                    {
                        _engine.SchedulePovBindPulse(pov.targetPov, angles[idx], "POV " + dirs[idx], 3.0, 6.0);
                    };
                    _pnlButtonsGrid.Controls.Add(povBtn);
                }
            }
        }

        private void OnAutoConfigureVJoyClick(object sender, EventArgs e)
        {
            if (_currentProfile == null || _currentProfile.vjoyTarget == null) return;
            string msg;
            bool ok = _vjoy.ConfigureDeviceForProfile(_currentProfile.vjoyTarget, out msg);
            UpdateStatusIndicators();
            MessageBox.Show(msg, ok ? "vJoy Configured Successfully" : "vJoy Configuration Notice",
                MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void StartWorkerThread()
        {
            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            _workerThread.Start();
        }

        private void WorkerLoop()
        {
            while (_running)
            {
                var profile = _currentProfile;
                int sleepMs = 4;

                if (profile != null)
                {
                    int hz = (profile.input != null && profile.input.pollingRateHz >= 30 && profile.input.pollingRateHz <= 1000)
                        ? profile.input.pollingRateHz
                        : 250;
                    sleepMs = Math.Max(1, 1000 / hz);

                    int ctrlIdx = (profile.input != null) ? profile.input.controllerIndex : 0;
                    string backend = (profile.input != null && !string.IsNullOrEmpty(profile.input.backend))
                        ? profile.input.backend
                        : "XInput";

                    NormalizedControllerState inputState;
                    if (string.Equals(backend, "WinMM", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(backend, "DirectInput", StringComparison.OrdinalIgnoreCase))
                    {
                        inputState = _winmmProvider.Poll(ctrlIdx);
                    }
                    else
                    {
                        inputState = _xinputProvider.Poll(ctrlIdx);
                        // Automatic fallback to WinMM if XInput is not found (e.g. GameSir T3 Lite in DInput mode)
                        if (!inputState.IsConnected)
                        {
                            var dinputTry = _winmmProvider.Poll(ctrlIdx);
                            // Make sure we don't accidentally poll vJoy itself if vJoy is JoyID 0 in WinMM:
                            // We only fallback to WinMM if explicitly set or if needed.
                        }
                    }

                    var outState = _engine.Evaluate(inputState, profile);
                    _vjoy.ApplyState(outState);

                    _latestInput = inputState;
                    _latestOutput = outState;
                }

                Thread.Sleep(sleepMs);
            }
        }

        private void OnUiTimerTick(object sender, EventArgs e)
        {
            UpdateStatusIndicators();

            var outSnap = _latestOutput;
            if (outSnap == null) return;

            foreach (var row in _axisRows)
            {
                int val;
                if (outSnap.Axes.TryGetValue(row.Rule.target, out val))
                {
                    row.Bar.Value = val;
                    int pct = (int)Math.Round(((val - VJoyOutput.AXIS_MIN) / (double)(VJoyOutput.AXIS_MAX - VJoyOutput.AXIS_MIN)) * 100.0);
                    if (row.LastPct != pct)
                    {
                        row.LastPct = pct;
                        row.ValueLabel.Text = string.Format("{0}%", pct);
                    }
                }
            }

            foreach (var cell in _buttonCells)
            {
                int btnIdx = cell.Rule.targetButton - 1;
                if (btnIdx >= 0 && btnIdx < outSnap.Buttons.Length)
                {
                    bool pressed = outSnap.Buttons[btnIdx];
                    Color targetColor = pressed ? Color.FromArgb(45, 165, 95) : Color.FromArgb(44, 48, 60);
                    if (cell.TriggerButton.BackColor != targetColor)
                    {
                        cell.TriggerButton.BackColor = targetColor;
                    }
                }
            }

            string pulseStatus = _engine.GetActivePulseStatus();
            if (!string.IsNullOrEmpty(pulseStatus))
            {
                _lblPulseBanner.Text = pulseStatus;
                _lblPulseBanner.BackColor = Color.FromArgb(165, 85, 25);
                _lblPulseBanner.ForeColor = Color.White;
            }
            else
            {
                _lblPulseBanner.Text = "💡 Tip: Click [Bind -] (Left/Down) or [Bind +] (Right/Up) below, then click the slot in Wardogs. (Click this banner anytime to stop pulsing)";
                _lblPulseBanner.BackColor = Color.FromArgb(40, 48, 66);
                _lblPulseBanner.ForeColor = Color.FromArgb(215, 228, 250);
            }
        }

        private void UpdateStatusIndicators()
        {
            var inp = _latestInput;
            if (inp != null && inp.IsConnected)
            {
                _lblInputStatus.Text = "Gamepad Connected (" + inp.BackendName + ")";
                _lblInputStatus.ForeColor = Color.FromArgb(95, 225, 140);
            }
            else
            {
                _lblInputStatus.Text = "Gamepad Disconnected (Turn on GameSir T3 Lite)";
                _lblInputStatus.ForeColor = Color.FromArgb(245, 175, 85);
            }

            if (_engine.IsPaused)
            {
                _btnTogglePause.Text = "⏸ Bridge PAUSED (Click or press Back+Start to Resume)";
                _btnTogglePause.BackColor = Color.FromArgb(145, 65, 65);
            }
            else
            {
                _btnTogglePause.Text = "▶ Bridge ACTIVE (Click or press Back+Start to Pause)";
                _btnTogglePause.BackColor = Color.FromArgb(42, 95, 68);
            }

            if (_currentProfile != null && _currentProfile.vjoyTarget != null)
            {
                uint devId = _currentProfile.vjoyTarget.deviceId;
                VjdStatus st = _vjoy.GetDeviceStatus(devId);
                int btns = _vjoy.GetButtonCount(devId);
                int povs = _vjoy.GetContPovCount(devId);
                bool meets = _vjoy.MeetsProfileRequirements(_currentProfile.vjoyTarget);

                _lblVJoyStatus.Text = string.Format("vJoy #{0}: {1} ({2} Buttons, {3} Continuous POV)", devId, st, btns, povs);
                if (meets && (st == VjdStatus.VJD_STAT_OWN || st == VjdStatus.VJD_STAT_FREE))
                {
                    _lblVJoyStatus.ForeColor = Color.FromArgb(95, 225, 140);
                    _btnAutoConfigureVJoy.BackColor = Color.FromArgb(55, 62, 78);
                    _btnAutoConfigureVJoy.Text = "Reconfigure vJoy #1";
                }
                else
                {
                    _lblVJoyStatus.ForeColor = Color.FromArgb(255, 185, 75);
                    _btnAutoConfigureVJoy.BackColor = Color.FromArgb(195, 95, 30);
                    _btnAutoConfigureVJoy.Text = string.Format("⚡ Upgrade vJoy #1 to {0} Btn + POV", _currentProfile.vjoyTarget.requiredButtons);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _running = false;
            _uiTimer.Stop();
            _vjoy.Dispose();
            base.OnFormClosing(e);
        }
    }
}
