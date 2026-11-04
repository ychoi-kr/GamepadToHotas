using System;
using System.Linq;
using System.Windows.Forms;

namespace GamepadToHotas
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args != null && args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            {
                return RunSelfTest();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static int RunSelfTest()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string defaultProfile = System.IO.Path.Combine(baseDir, "profiles", "gamesir_t3_lite_mode2.json");
                var profile = ControllerProfile.LoadFromFile(defaultProfile);

                var engine = new MappingEngine();
                var mockInput = new NormalizedControllerState { IsConnected = true, BackendName = "MockXInput" };
                mockInput.Axes["LeftThumbY"] = 0.5;   // Collective 75%
                mockInput.Axes["RightThumbX"] = -0.5; // Roll 25%
                mockInput.PressedButtons.Add("A");
                mockInput.PressedButtons.Add("DPadUp");

                var snap = engine.Evaluate(mockInput, profile);
                Console.WriteLine("Profile loaded: " + profile.displayName);
                Console.WriteLine("Evaluated Z (Collective): " + snap.Axes["Z"]);
                Console.WriteLine("Evaluated X (Roll): " + snap.Axes["X"]);
                Console.WriteLine("Button 1 (A) pressed: " + snap.Buttons[0]);
                Console.WriteLine("POV 1 angle: " + snap.ContPovs[0]);

                using (var vjoy = new VJoyOutput())
                {
                    Console.WriteLine("vJoy Driver Enabled: " + vjoy.IsDriverEnabled());
                    Console.WriteLine("vJoy #1 Status: " + vjoy.GetDeviceStatus(1));
                    if (vjoy.IsDriverEnabled())
                    {
                        vjoy.Acquire(1);
                    }
                }

                string reportPath = System.IO.Path.Combine(baseDir, "selftest_result.txt");
                System.IO.File.WriteAllText(reportPath, string.Format(
                    "OK | Profile={0} | Z={1} | X={2} | Rx={3} | Ry={4} | Btn1={5} | POV1={6}",
                    profile.profileId, snap.Axes["Z"], snap.Axes["X"], snap.Axes["Rx"], snap.Axes["Ry"], snap.Buttons[0], snap.ContPovs[0]));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Self-test failed: " + ex);
                return 1;
            }
        }
    }
}
