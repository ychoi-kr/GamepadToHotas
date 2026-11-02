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
