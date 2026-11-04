using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace GamepadToHotas
{
    public enum VjdStatus : int
    {
        VJD_STAT_OWN = 0,  // Owned by this application
        VJD_STAT_FREE = 1, // Free to be acquired
        VJD_STAT_BUSY = 2, // Owned by another application
        VJD_STAT_MISS = 3, // Missing / Not configured
        VJD_STAT_UNKN = 4  // Unknown / Error
    }

    public class VJoyStateSnapshot
    {
        public Dictionary<string, int> Axes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public bool[] Buttons = new bool[32]; // 0-indexed for Buttons 1..32
        public int[] ContPovs = new int[4] { -1, -1, -1, -1 }; // -1 = neutral, 0..35900 = hundredths of degree
    }

    /// <summary>
    /// Wraps BrunnerInnovation/vJoy's `vJoyInterface.dll` and `vJoyConfig.exe`.
    /// Automatically adds `C:\Program Files\vJoy\x64` to the DLL search path.
    /// </summary>
    public class VJoyOutput : IDisposable
    {
        public const int AXIS_MIN = 0x1;
        public const int AXIS_MID = 0x4000; // 16384
        public const int AXIS_MAX = 0x8000; // 32768

        public static readonly Dictionary<string, uint> HidUsageMap = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            { "X",   0x30 },
            { "Y",   0x31 },
            { "Z",   0x32 },
            { "Rx",  0x33 },
            { "Ry",  0x34 },
            { "Rz",  0x35 },
            { "Sl0", 0x36 },
            { "Sl1", 0x37 }
        };

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool vJoyEnabled();

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetVJDStatus(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetVJDButtonNumber(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetVJDContPovNumber(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool AcquireVJD(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RelinquishVJD(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ResetVJD(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SetAxis(int Value, uint rID, uint Axis);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SetBtn(bool Value, uint rID, byte nBtn);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SetContPov(int Value, uint rID, byte nPov);

        private uint _acquiredDeviceId = 0;

        public static string VJoyFolder
        {
            get { return @"C:\Program Files\vJoy\x64"; }
        }

        static VJoyOutput()
        {
            if (Directory.Exists(VJoyFolder))
            {
                SetDllDirectory(VJoyFolder);
            }
        }

        public bool IsDriverEnabled()
        {
            try { return vJoyEnabled(); }
            catch { return false; }
        }

        public VjdStatus GetDeviceStatus(uint deviceId)
        {
            try { return (VjdStatus)GetVJDStatus(deviceId); }
            catch { return VjdStatus.VJD_STAT_UNKN; }
        }

        public int GetButtonCount(uint deviceId)
        {
            try { return GetVJDButtonNumber(deviceId); }
            catch { return 0; }
        }

        public int GetContPovCount(uint deviceId)
        {
            try { return GetVJDContPovNumber(deviceId); }
            catch { return 0; }
        }

        public uint AcquiredDeviceId
        {
            get { return _acquiredDeviceId; }
        }

        public bool Acquire(uint deviceId)
        {
            if (_acquiredDeviceId == deviceId) return true;
            Relinquish();

            VjdStatus status = GetDeviceStatus(deviceId);
            if (status == VjdStatus.VJD_STAT_OWN || status == VjdStatus.VJD_STAT_FREE)
            {
                if (AcquireVJD(deviceId))
                {
                    _acquiredDeviceId = deviceId;
                    ResetToNeutral(deviceId);
                    return true;
                }
            }
            return false;
        }

        public void ResetToNeutral(uint deviceId)
        {
            if (deviceId == 0) return;
            try
            {
                ResetVJD(deviceId);
                foreach (var kvp in HidUsageMap)
                {
                    SetAxis(AXIS_MID, deviceId, kvp.Value);
                }
                for (byte b = 1; b <= 32; b++)
                {
                    SetBtn(false, deviceId, b);
                }
                for (byte p = 1; p <= 4; p++)
                {
                    SetContPov(-1, deviceId, p);
                }
            }
            catch { }
        }

        public void Relinquish()
        {
            if (_acquiredDeviceId != 0)
            {
                try
                {
                    ResetToNeutral(_acquiredDeviceId);
                    RelinquishVJD(_acquiredDeviceId);
                }
                catch { }
                _acquiredDeviceId = 0;
            }
        }

        public void ApplyState(VJoyStateSnapshot state)
        {
            if (_acquiredDeviceId == 0) return;

            foreach (var kvp in state.Axes)
            {
                uint usage;
                if (HidUsageMap.TryGetValue(kvp.Key, out usage))
                {
                    SetAxis(kvp.Value, _acquiredDeviceId, usage);
                }
            }

            for (int i = 0; i < state.Buttons.Length; i++)
            {
                SetBtn(state.Buttons[i], _acquiredDeviceId, (byte)(i + 1));
            }

            for (int p = 0; p < state.ContPovs.Length; p++)
            {
                SetContPov(state.ContPovs[p], _acquiredDeviceId, (byte)(p + 1));
            }
        }

        /// <summary>
        /// Checks if the target vJoy device meets the profile's requirements (buttons, POVs, axes).
        /// </summary>
        public bool MeetsProfileRequirements(VJoyTargetConfig cfg)
        {
            VjdStatus st = GetDeviceStatus(cfg.deviceId);
            if (st != VjdStatus.VJD_STAT_FREE && st != VjdStatus.VJD_STAT_OWN)
                return false;

            int btns = GetButtonCount(cfg.deviceId);
            int povs = GetContPovCount(cfg.deviceId);
            return btns >= cfg.requiredButtons && povs >= cfg.requiredContPovs;
        }

        /// <summary>
        /// Invokes `vJoyConfig.exe` (with Administrator elevation if required) to configure the virtual HOTAS device.
        /// Example command executed: `vJoyConfig.exe 1 -f -a X Y Z Rx Ry Rz Sl0 Sl1 -b 16 -p 1`
        /// </summary>
        public bool ConfigureDeviceForProfile(VJoyTargetConfig cfg, out string statusMessage)
        {
            string exePath = Path.Combine(VJoyFolder, "vJoyConfig.exe");
            if (!File.Exists(exePath))
            {
                statusMessage = "vJoyConfig.exe not found at " + exePath;
                return false;
            }

            Relinquish();

            string axesList = (cfg.requiredAxes != null && cfg.requiredAxes.Count > 0)
                ? string.Join(" ", cfg.requiredAxes.ToArray())
                : "X Y Z Rx Ry Rz Sl0 Sl1";

            string args = string.Format("{0} -f -a {1} -b {2} -p {3}",
                cfg.deviceId, axesList, cfg.requiredButtons, cfg.requiredContPovs);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    Verb = "runas", // Request UAC elevation so vJoy driver can re-enumerate PnP device
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        proc.WaitForExit(10000);
                    }
                }

                System.Threading.Thread.Sleep(600);
                Acquire(cfg.deviceId);
                statusMessage = string.Format("Configured vJoy #{0}: {1} buttons, {2} POV, Axes [{3}]",
                    cfg.deviceId, GetButtonCount(cfg.deviceId), GetContPovCount(cfg.deviceId), axesList);
                return MeetsProfileRequirements(cfg);
            }
            catch (Exception ex)
            {
                statusMessage = "vJoyConfig execution canceled or failed: " + ex.Message;
                return false;
            }
        }

        public void Dispose()
        {
            Relinquish();
        }
    }
}
