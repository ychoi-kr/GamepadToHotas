using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GamepadToHotas
{
    /// <summary>
    /// Normalized hardware-agnostic snapshot of a physical controller state.
    /// All axis values are normalized in [-1.0, +1.0] where 0.0 is center (for sticks)
    /// or [-1.0 = released, +1.0 = full press] when mapped to full vJoy range.
    /// </summary>
    public class NormalizedControllerState
    {
        public bool IsConnected { get; set; }
        public string BackendName { get; set; }
        public Dictionary<string, double> Axes { get; private set; }
        public HashSet<string> PressedButtons { get; private set; }

        public NormalizedControllerState()
        {
            Axes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            PressedButtons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public double GetAxis(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0.0;
            double val;
            return Axes.TryGetValue(name, out val) ? val : 0.0;
        }

        public bool IsPressed(string buttonName)
        {
            if (string.IsNullOrEmpty(buttonName)) return false;
            return PressedButtons.Contains(buttonName);
        }
    }

    public interface IInputProvider
    {
        NormalizedControllerState Poll(int controllerIndex);
    }

    /// <summary>
    /// Polls standard XInput gamepads (GameSir T3 Lite in XInput mode, Xbox controllers, etc.)
    /// Automatically falls back across XInput1_4.dll and xinput9_1_0.dll.
    /// </summary>
    public class XInputProvider : IInputProvider
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [DllImport("XInput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState910(uint dwUserIndex, out XINPUT_STATE pState);

        private static uint SafeXInputGetState(uint index, out XINPUT_STATE state)
        {
            try
            {
                return XInputGetState14(index, out state);
            }
            catch
            {
                return XInputGetState910(index, out state);
            }
        }

        public NormalizedControllerState Poll(int controllerIndex)
        {
            var result = new NormalizedControllerState { BackendName = "XInput" };
            XINPUT_STATE state;

            // If requested index is disconnected, also check index 0..3 as auto-fallback if index == 0
            uint res = SafeXInputGetState((uint)controllerIndex, out state);
            int activeIdx = controllerIndex;

            if (res != 0 && controllerIndex == 0)
            {
                for (int i = 1; i < 4; i++)
                {
                    if (SafeXInputGetState((uint)i, out state) == 0)
                    {
                        res = 0;
                        activeIdx = i;
                        break;
                    }
                }
            }

            if (res != 0)
            {
                result.IsConnected = false;
                return result;
            }

            result.IsConnected = true;
            result.BackendName = "XInput #" + activeIdx;

            var gp = state.Gamepad;
            double lx = NormalizeShort(gp.sThumbLX);
            double ly = NormalizeShort(gp.sThumbLY);
            double rx = NormalizeShort(gp.sThumbRX);
            double ry = NormalizeShort(gp.sThumbRY);

            // Triggers mapped to [0.0 = neutral center (50%), +1.0 = full press (100%)]
            // so released triggers remain at vJoy neutral (16384) instead of pulling axes to -100% (1).
            double ltNorm01 = gp.bLeftTrigger / 255.0;
            double rtNorm01 = gp.bRightTrigger / 255.0;

            result.Axes["LeftThumbX"] = lx;
            result.Axes["LeftThumbY"] = ly;
            result.Axes["RightThumbX"] = rx;
            result.Axes["RightThumbY"] = ry;
            result.Axes["LeftTrigger"] = ltNorm01;
            result.Axes["RightTrigger"] = rtNorm01;
            result.Axes["TriggersCombined"] = rtNorm01 - ltNorm01;

            ushort b = gp.wButtons;
            if ((b & 0x0001) != 0) result.PressedButtons.Add("DPadUp");
            if ((b & 0x0002) != 0) result.PressedButtons.Add("DPadDown");
            if ((b & 0x0004) != 0) result.PressedButtons.Add("DPadLeft");
            if ((b & 0x0008) != 0) result.PressedButtons.Add("DPadRight");
            if ((b & 0x0010) != 0) result.PressedButtons.Add("Start");
            if ((b & 0x0020) != 0) result.PressedButtons.Add("Back");
            if ((b & 0x0040) != 0) result.PressedButtons.Add("LeftThumb");
            if ((b & 0x0080) != 0) result.PressedButtons.Add("RightThumb");
            if ((b & 0x0100) != 0) result.PressedButtons.Add("LeftShoulder");
            if ((b & 0x0200) != 0) result.PressedButtons.Add("RightShoulder");
            if ((b & 0x1000) != 0) result.PressedButtons.Add("A");
            if ((b & 0x2000) != 0) result.PressedButtons.Add("B");
            if ((b & 0x4000) != 0) result.PressedButtons.Add("X");
            if ((b & 0x8000) != 0) result.PressedButtons.Add("Y");

            if (gp.bLeftTrigger > 128) result.PressedButtons.Add("LeftTriggerButton");
            if (gp.bRightTrigger > 128) result.PressedButtons.Add("RightTriggerButton");

            return result;
        }

        private static double NormalizeShort(short val)
        {
            if (val >= 0) return val / 32767.0;
            return val / 32768.0;
        }
    }

    /// <summary>
    /// Fallback DirectInput / WinMM Joystick provider for controllers running in DInput mode.
    /// </summary>
    public class WinMMInputProvider : IInputProvider
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct JOYINFOEX
        {
            public uint dwSize;
            public uint dwFlags;
            public uint dwXpos;
            public uint dwYpos;
            public uint dwZpos;
            public uint dwRpos;
            public uint dwUpos;
            public uint dwVpos;
            public uint dwButtons;
            public uint dwButtonNumber;
            public uint dwPOV;
            public uint dwReserved1;
            public uint dwReserved2;
        }

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

        private const uint JOY_RETURNALL = 0x000000FF;

        public NormalizedControllerState Poll(int controllerIndex)
        {
            var result = new NormalizedControllerState { BackendName = "WinMM/DInput" };
            JOYINFOEX info = new JOYINFOEX();
            info.dwSize = (uint)Marshal.SizeOf(typeof(JOYINFOEX));
            info.dwFlags = JOY_RETURNALL;

            uint res = joyGetPosEx((uint)controllerIndex, ref info);
            if (res != 0)
            {
                result.IsConnected = false;
                return result;
            }

            result.IsConnected = true;
            result.BackendName = "WinMM #" + controllerIndex;

            double ax0 = NormalizeUInt16(info.dwXpos);
            double ax1 = -NormalizeUInt16(info.dwYpos); // Y is usually top=0 in WinMM
            double ax2 = NormalizeUInt16(info.dwZpos);
            double ax3 = -NormalizeUInt16(info.dwRpos);
            double ax4 = NormalizeUInt16(info.dwUpos);
            double ax5 = NormalizeUInt16(info.dwVpos);

            result.Axes["Axis0"] = ax0;
            result.Axes["Axis1"] = ax1;
            result.Axes["Axis2"] = ax2;
            result.Axes["Axis3"] = ax3;
            result.Axes["Axis4"] = ax4;
            result.Axes["Axis5"] = ax5;

            // Also provide standard aliases so the same profile works seamlessly.
            // Clamp triggers to [0.0, +1.0] so unpressed or missing WinMM axes rest at neutral (0.0 -> 50%).
            double ltWinMM = Math.Max(0.0, ax4);
            double rtWinMM = Math.Max(0.0, ax5);
            result.Axes["LeftThumbX"] = ax0;
            result.Axes["LeftThumbY"] = ax1;
            result.Axes["RightThumbX"] = ax2;
            result.Axes["RightThumbY"] = ax3;
            result.Axes["LeftTrigger"] = ltWinMM;
            result.Axes["RightTrigger"] = rtWinMM;
            result.Axes["TriggersCombined"] = rtWinMM - ltWinMM;

            for (int i = 0; i < 32; i++)
            {
                if ((info.dwButtons & (1u << i)) != 0)
                {
                    result.PressedButtons.Add("Button" + (i + 1));
                }
            }

            // Standard button aliases for common DInput pads
            if (result.PressedButtons.Contains("Button1")) result.PressedButtons.Add("A");
            if (result.PressedButtons.Contains("Button2")) result.PressedButtons.Add("B");
            if (result.PressedButtons.Contains("Button3")) result.PressedButtons.Add("X");
            if (result.PressedButtons.Contains("Button4")) result.PressedButtons.Add("Y");
            if (result.PressedButtons.Contains("Button5")) result.PressedButtons.Add("LeftShoulder");
            if (result.PressedButtons.Contains("Button6")) result.PressedButtons.Add("RightShoulder");
            if (result.PressedButtons.Contains("Button7")) result.PressedButtons.Add("Back");
            if (result.PressedButtons.Contains("Button8")) result.PressedButtons.Add("Start");
            if (result.PressedButtons.Contains("Button9")) result.PressedButtons.Add("LeftThumb");
            if (result.PressedButtons.Contains("Button10")) result.PressedButtons.Add("RightThumb");

            // POV Hat in hundredths of a degree (0..35900, 65535 = centered)
            if (info.dwPOV < 36000)
            {
                uint deg = info.dwPOV / 100;
                if (deg >= 315 || deg <= 45) result.PressedButtons.Add("DPadUp");
                if (deg >= 45 && deg <= 135) result.PressedButtons.Add("DPadRight");
                if (deg >= 135 && deg <= 225) result.PressedButtons.Add("DPadDown");
                if (deg >= 225 && deg <= 315) result.PressedButtons.Add("DPadLeft");
            }

            return result;
        }

        private static double NormalizeUInt16(uint val)
        {
            double clamped = Math.Max(0.0, Math.Min(65535.0, val));
            return ((clamped / 65535.0) * 2.0) - 1.0;
        }
    }
}
