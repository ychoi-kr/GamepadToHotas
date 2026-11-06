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

                // 1. Test idle state (LeftThumbY = 0.0 -> Btn 14 & Btn 15 both OFF, Z = 16384 neutral)
                var snapIdle = engine.Evaluate(mockInput, profile);
                bool idleBtn14 = snapIdle.Buttons[13];
                bool idleBtn15 = snapIdle.Buttons[14];

                // 2. Push LeftThumbY up to +1.0 -> Btn 15 (Collective Up) ON, Btn 14 OFF
                mockInput.Axes["LeftThumbY"] = 1.0;
                mockInput.Axes["RightThumbX"] = -0.5; // Roll 25%
                mockInput.PressedButtons.Add("A");
                mockInput.PressedButtons.Add("DPadUp");
                var snapUp = engine.Evaluate(mockInput, profile);

                // 3. Pull LeftThumbY down to -1.0 -> Btn 15 OFF, Btn 14 (Collective Down) ON
                mockInput.Axes["LeftThumbY"] = -1.0;
                var snapDown = engine.Evaluate(mockInput, profile);

                Console.WriteLine("Profile loaded: " + profile.displayName);
                Console.WriteLine("Idle Btn14/Btn15: " + idleBtn14 + "/" + idleBtn15);
                Console.WriteLine("Up Btn14/Btn15: " + snapUp.Buttons[13] + "/" + snapUp.Buttons[14]);
                Console.WriteLine("Down Btn14/Btn15: " + snapDown.Buttons[13] + "/" + snapDown.Buttons[14]);
                Console.WriteLine("Evaluated X (Roll): " + snapUp.Axes["X"]);

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
                    "OK | Profile={0} | Z={1} | X={2} | Idle14_15={3}_{4} | Up15={5} | Down14={6} | Btn1={7} | POV1={8}",
                    profile.profileId, snapUp.Axes["Z"], snapUp.Axes["X"], idleBtn14, idleBtn15, snapUp.Buttons[14], snapDown.Buttons[13], snapUp.Buttons[0], snapUp.ContPovs[0]));
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
