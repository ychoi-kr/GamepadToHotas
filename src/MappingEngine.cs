using System;
using System.Collections.Generic;

namespace GamepadToHotas
{
    /// <summary>
    /// Represents a scheduled "Solo Output" pulse for in-game control binding.
    /// When the user wants to bind an axis or button inside Wardogs' HOTAS settings,
    /// they click a button in our app, switch to the game window within the countdown (e.g., 3 seconds),
    /// and our engine emits ONLY the virtual vJoy axis/button movement without the user touching the physical pad.
    /// </summary>
    public class SoloBindingPulse
    {
        public string Description { get; set; }
        public string TargetAxis { get; set; }
        public int TargetButton { get; set; }
        public int TargetPovNumber { get; set; }
        public int TargetPovAngle { get; set; }
        public DateTime CreatedUtc { get; set; }
        public double DelaySeconds { get; set; }
        public double DurationSeconds { get; set; }
    }

    /// <summary>
    /// Pure mapping engine that transforms a `NormalizedControllerState` + `ControllerProfile`
    /// into a `VJoyStateSnapshot`.
    /// </summary>
    public class MappingEngine
    {
        public bool IsPaused { get; set; }
        private bool _comboWasDown = false;
        private SoloBindingPulse _activePulse = null;

        public void ScheduleAxisBindPulse(string axisTarget, string label, double delaySec = 3.0, double durationSec = 1.5)
        {
            _activePulse = new SoloBindingPulse
            {
                Description = "Binding Axis [" + axisTarget + "] (" + label + ")",
                TargetAxis = axisTarget,
                CreatedUtc = DateTime.UtcNow,
                DelaySeconds = delaySec,
                DurationSeconds = durationSec
            };
        }

        public void ScheduleButtonBindPulse(int buttonNumber, string label, double delaySec = 3.0, double durationSec = 0.8)
        {
            _activePulse = new SoloBindingPulse
            {
                Description = "Binding Button #" + buttonNumber + " (" + label + ")",
                TargetButton = buttonNumber,
                CreatedUtc = DateTime.UtcNow,
                DelaySeconds = delaySec,
                DurationSeconds = durationSec
            };
        }

        public void SchedulePovBindPulse(int povNumber, int angleHundredths, string label, double delaySec = 3.0, double durationSec = 0.8)
        {
            _activePulse = new SoloBindingPulse
            {
                Description = "Binding POV #" + povNumber + " (" + label + ")",
                TargetPovNumber = povNumber,
                TargetPovAngle = angleHundredths,
                CreatedUtc = DateTime.UtcNow,
                DelaySeconds = delaySec,
                DurationSeconds = durationSec
            };
        }

        public string GetActivePulseStatus()
        {
            var pulse = _activePulse;
            if (pulse == null) return null;

            double elapsed = (DateTime.UtcNow - pulse.CreatedUtc).TotalSeconds;
            if (elapsed < pulse.DelaySeconds)
            {
                double remain = pulse.DelaySeconds - elapsed;
                return string.Format("⏳ {0} in {1:0.0}s... (Click in-game bind slot now!)", pulse.Description, remain);
            }
            if (elapsed < pulse.DelaySeconds + pulse.DurationSeconds)
            {
                return string.Format("⚡ PULSING NOW: {0}!", pulse.Description);
            }

            _activePulse = null;
            return null;
        }

        public VJoyStateSnapshot Evaluate(NormalizedControllerState input, ControllerProfile profile)
        {
            var outState = new VJoyStateSnapshot();

            // Initialize all configured axes to center (16384)
            foreach (var axName in VJoyOutput.HidUsageMap.Keys)
            {
                outState.Axes[axName] = VJoyOutput.AXIS_MID;
            }

            if (profile == null) return outState;

            // Check passthrough / pause toggle combo (e.g., Back + Start)
            if (input != null && input.IsConnected && profile.input != null &&
                profile.input.passthroughToggleCombo != null && profile.input.passthroughToggleCombo.Count >= 2)
            {
                bool allDown = true;
                foreach (var btn in profile.input.passthroughToggleCombo)
                {
                    if (!input.IsPressed(btn))
                    {
                        allDown = false;
                        break;
                    }
                }

                if (allDown && !_comboWasDown)
                {
                    IsPaused = !IsPaused;
                    System.Media.SystemSounds.Beep.Play();
                }
                _comboWasDown = allDown;
            }
            else
            {
                _comboWasDown = false;
            }

            // Check if a Solo Binding Pulse is currently active
            var pulse = _activePulse;
            if (pulse != null)
            {
                double elapsed = (DateTime.UtcNow - pulse.CreatedUtc).TotalSeconds;
                if (elapsed < pulse.DelaySeconds)
                {
                    // During countdown, hold everything strictly neutral so physical controller noise is ignored
                    return outState;
                }
                else if (elapsed < pulse.DelaySeconds + pulse.DurationSeconds)
                {
                    // During pulse window, oscillate the target axis or press the target button
                    if (!string.IsNullOrEmpty(pulse.TargetAxis))
                    {
                        double phase = (elapsed - pulse.DelaySeconds) / pulse.DurationSeconds;
                        int val = (phase < 0.5) ? VJoyOutput.AXIS_MAX : VJoyOutput.AXIS_MIN;
                        outState.Axes[pulse.TargetAxis] = val;
                    }
                    else if (pulse.TargetButton >= 1 && pulse.TargetButton <= 32)
                    {
                        outState.Buttons[pulse.TargetButton - 1] = true;
                    }
                    else if (pulse.TargetPovNumber >= 1 && pulse.TargetPovNumber <= 4)
                    {
                        outState.ContPovs[pulse.TargetPovNumber - 1] = pulse.TargetPovAngle;
                    }
                    return outState;
                }
                else
                {
                    _activePulse = null;
                }
            }

            if (IsPaused || input == null || !input.IsConnected)
            {
                return outState;
            }

            // 1. Evaluate Axes
            if (profile.axes != null)
            {
                foreach (var rule in profile.axes)
                {
                    if (string.IsNullOrEmpty(rule.target)) continue;
                    double raw = input.GetAxis(rule.source);

                    // Apply optional deadzone (default 0.0 so Wardogs handles deadzone natively)
                    if (rule.deadzone > 0.0 && rule.deadzone < 0.95)
                    {
                        if (Math.Abs(raw) <= rule.deadzone)
                            raw = 0.0;
                        else
                            raw = Math.Sign(raw) * ((Math.Abs(raw) - rule.deadzone) / (1.0 - rule.deadzone));
                    }

                    if (rule.invert)
                    {
                        raw = -raw;
                    }

                    raw = Math.Max(-1.0, Math.Min(1.0, raw));

                    // Convert [-1.0, +1.0] to vJoy [1, 32768] centered at 16384
                    int vjoyVal = (int)Math.Round(VJoyOutput.AXIS_MID + raw * (VJoyOutput.AXIS_MAX - VJoyOutput.AXIS_MID));
                    if (vjoyVal < VJoyOutput.AXIS_MIN) vjoyVal = VJoyOutput.AXIS_MIN;
                    if (vjoyVal > VJoyOutput.AXIS_MAX) vjoyVal = VJoyOutput.AXIS_MAX;

                    outState.Axes[rule.target] = vjoyVal;
                }
            }

            // 2. Evaluate Buttons
            if (profile.buttons != null)
            {
                foreach (var bRule in profile.buttons)
                {
                    if (bRule.targetButton >= 1 && bRule.targetButton <= 32)
                    {
                        if (input.IsPressed(bRule.source))
                        {
                            outState.Buttons[bRule.targetButton - 1] = true;
                        }
                    }
                }
            }

            // 3. Evaluate Continuous POV Hat switches (0..35900 hundredths of a degree, -1 = neutral)
            if (profile.povs != null)
            {
                foreach (var pRule in profile.povs)
                {
                    if (pRule.targetPov < 1 || pRule.targetPov > 4) continue;
                    bool u = input.IsPressed(pRule.up);
                    bool r = input.IsPressed(pRule.right);
                    bool d = input.IsPressed(pRule.down);
                    bool l = input.IsPressed(pRule.left);

                    int povVal = -1;
                    if (u && r) povVal = 4500;
                    else if (r && d) povVal = 13500;
                    else if (d && l) povVal = 22500;
                    else if (l && u) povVal = 31500;
                    else if (u) povVal = 0;
                    else if (r) povVal = 9000;
                    else if (d) povVal = 18000;
                    else if (l) povVal = 27000;

                    outState.ContPovs[pRule.targetPov - 1] = povVal;
                }
            }

            return outState;
        }
    }
}
