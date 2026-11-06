using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace GamepadToHotas
{
    /// <summary>
    /// Root declarative schema for a Gamepad-to-vJoy HOTAS mapping profile.
    /// Matches files in the `profiles/*.json` directory.
    /// 
    /// Supported Axis Sources (`AxisRule.source`):
    ///   - Standard XInput/Gamepad: "LeftThumbX", "LeftThumbY", "RightThumbX", "RightThumbY",
    ///     "LeftTrigger", "RightTrigger", "TriggersCombined" (RT minus LT centered at 50%)
    ///   - Generic WinMM / DirectInput Axes: "Axis0" through "Axis5" (X, Y, Z, R, U, V)
    /// 
    /// Supported vJoy Target Axes (`AxisRule.target`):
    ///   - "X", "Y", "Z", "Rx", "Ry", "Rz", "Sl0", "Sl1"
    /// 
    /// Supported Button Sources (`ButtonRule.source` / `PovRule` directions):
    ///   - Standard XInput: "A", "B", "X", "Y", "LeftShoulder", "RightShoulder",
    ///     "Back", "Start", "LeftThumb", "RightThumb",
    ///     "DPadUp", "DPadRight", "DPadDown", "DPadLeft",
    ///     "LeftTriggerButton", "RightTriggerButton"
    ///   - Stick Proportional PWM Button Sources: "LeftThumbYUp", "LeftThumbYDown"
    ///   - Generic WinMM / DirectInput Buttons: "Button1" through "Button32"
    /// </summary>
    public class ControllerProfile
    {
        public string profileId { get; set; }
        public string displayName { get; set; }
        public string description { get; set; }
        public InputConfig input { get; set; }
        public VJoyTargetConfig vjoyTarget { get; set; }
        public List<AxisRule> axes { get; set; }
        public List<PovRule> povs { get; set; }
        public List<ButtonRule> buttons { get; set; }

        [ScriptIgnore]
        public string FilePath { get; set; }

        public ControllerProfile()
        {
            input = new InputConfig();
            vjoyTarget = new VJoyTargetConfig();
            axes = new List<AxisRule>();
            povs = new List<PovRule>();
            buttons = new List<ButtonRule>();
        }

        public static ControllerProfile LoadFromFile(string path)
        {
            string json = File.ReadAllText(path);
            var serializer = new JavaScriptSerializer();
            var profile = serializer.Deserialize<ControllerProfile>(json);
            profile.FilePath = path;
            return profile;
        }

        public static ControllerProfile CreateDefaultMode2Profile()
        {
            var p = new ControllerProfile
            {
                profileId = "gamesir_t3_lite_mode2",
                displayName = "GameSir T3 Lite / XInput - Helicopter Mode 2",
                description = "Maps a standard gamepad (such as GameSir T3 Lite) to a vJoy HOTAS device in Helicopter Mode 2 (Left Stick: Collective/Yaw, Right Stick: Cyclic Pitch/Roll)."
            };
            p.axes.Add(new AxisRule { name = "Cyclic Roll (Right Stick X)", source = "RightThumbX", target = "X", invert = false, deadzone = 0.0 });
            p.axes.Add(new AxisRule { name = "Cyclic Pitch (Right Stick Y)", source = "RightThumbY", target = "Y", invert = true, deadzone = 0.0 });
            p.axes.Add(new AxisRule { name = "Yaw / Rudder (Left Stick X)", source = "LeftThumbX", target = "Rz", invert = false, deadzone = 0.0 });
            p.axes.Add(new AxisRule { name = "Left Trigger Axis (LT)", source = "LeftTrigger", target = "Rx", invert = false, deadzone = 0.0 });
            p.axes.Add(new AxisRule { name = "Right Trigger Axis (RT)", source = "RightTrigger", target = "Ry", invert = false, deadzone = 0.0 });

            p.povs.Add(new PovRule { name = "D-Pad to HAT POV 1", targetPov = 1, up = "DPadUp", right = "DPadRight", down = "DPadDown", left = "DPadLeft" });

            p.buttons.Add(new ButtonRule { targetButton = 1, source = "A", label = "A (Cross)" });
            p.buttons.Add(new ButtonRule { targetButton = 2, source = "B", label = "B (Circle)" });
            p.buttons.Add(new ButtonRule { targetButton = 3, source = "X", label = "X (Square)" });
            p.buttons.Add(new ButtonRule { targetButton = 4, source = "Y", label = "Y (Triangle)" });
            p.buttons.Add(new ButtonRule { targetButton = 5, source = "LeftShoulder", label = "LB (L1)" });
            p.buttons.Add(new ButtonRule { targetButton = 6, source = "RightShoulder", label = "RB (R1)" });
            p.buttons.Add(new ButtonRule { targetButton = 7, source = "Back", label = "Back / Select" });
            p.buttons.Add(new ButtonRule { targetButton = 8, source = "Start", label = "Start / Menu" });
            p.buttons.Add(new ButtonRule { targetButton = 9, source = "LeftThumb", label = "L3 (Left Stick Click)" });
            p.buttons.Add(new ButtonRule { targetButton = 10, source = "RightThumb", label = "R3 (Right Stick Click)" });
            p.buttons.Add(new ButtonRule { targetButton = 11, source = "LeftTriggerButton", label = "LT Digital Click (>50%)" });
            p.buttons.Add(new ButtonRule { targetButton = 12, source = "RightTriggerButton", label = "RT Digital Click (>50%)" });
            p.buttons.Add(new ButtonRule { targetButton = 13, source = "DPadUp", label = "D-Pad Up (Button)" });
            p.buttons.Add(new ButtonRule { targetButton = 14, source = "LeftThumbYDown", label = "Collective Down (LS Down PWM)" });
            p.buttons.Add(new ButtonRule { targetButton = 15, source = "LeftThumbYUp", label = "Collective Up (LS Up PWM)" });
            p.buttons.Add(new ButtonRule { targetButton = 16, source = "DPadLeft", label = "D-Pad Left (Button)" });
            return p;
        }

        public void SaveToFile(string path = null)
        {
            string targetPath = path ?? FilePath;
            if (string.IsNullOrEmpty(targetPath))
                throw new InvalidOperationException("No file path specified for saving profile.");

            var serializer = new JavaScriptSerializer();
            string rawJson = serializer.Serialize(this);
            string formatted = FormatJson(rawJson);
            File.WriteAllText(targetPath, formatted);
            FilePath = targetPath;
        }

        private static string FormatJson(string json)
        {
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;
            int indent = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    sb.Append(ch);
                }
                else if (!inQuotes)
                {
                    if (ch == '{' || ch == '[')
                    {
                        sb.Append(ch);
                        sb.AppendLine();
                        indent++;
                        sb.Append(new string(' ', indent * 2));
                    }
                    else if (ch == '}' || ch == ']')
                    {
                        sb.AppendLine();
                        indent--;
                        sb.Append(new string(' ', Math.Max(0, indent) * 2));
                        sb.Append(ch);
                    }
                    else if (ch == ',')
                    {
                        sb.Append(ch);
                        sb.AppendLine();
                        sb.Append(new string(' ', indent * 2));
                    }
                    else if (ch == ':')
                    {
                        sb.Append(": ");
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }
    }

    public class InputConfig
    {
        public string backend { get; set; }
        public int controllerIndex { get; set; }
        public int pollingRateHz { get; set; }
        public List<string> passthroughToggleCombo { get; set; }

        public InputConfig()
        {
            backend = "XInput";
            controllerIndex = 0;
            pollingRateHz = 250;
            passthroughToggleCombo = new List<string> { "Back", "Start" };
        }
    }

    public class VJoyTargetConfig
    {
        public uint deviceId { get; set; }
        public int requiredButtons { get; set; }
        public int requiredContPovs { get; set; }
        public List<string> requiredAxes { get; set; }

        public VJoyTargetConfig()
        {
            deviceId = 1;
            requiredButtons = 16;
            requiredContPovs = 1;
            requiredAxes = new List<string> { "X", "Y", "Z", "Rx", "Ry", "Rz", "Sl0", "Sl1" };
        }
    }

    public class AxisRule
    {
        public string name { get; set; }
        public string source { get; set; }
        public string target { get; set; }
        public bool invert { get; set; }
        public double deadzone { get; set; }
        public bool groundLatch { get; set; }
    }

    public class PovRule
    {
        public string name { get; set; }
        public int targetPov { get; set; }
        public string up { get; set; }
        public string right { get; set; }
        public string down { get; set; }
        public string left { get; set; }

        public PovRule()
        {
            targetPov = 1;
            up = "DPadUp";
            right = "DPadRight";
            down = "DPadDown";
            left = "DPadLeft";
        }
    }

    public class ButtonRule
    {
        public int targetButton { get; set; }
        public string source { get; set; }
        public string label { get; set; }
    }
}
