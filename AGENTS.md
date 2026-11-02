# Agent Context & Guidelines

Use these notes when modifying code or creating controller profiles in this repository:

1. **Compiler & Language Version Constraint (C# 5)**:
   - Built with the inbox Windows compiler (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) via `build.ps1` so users never need to install a .NET SDK or Visual Studio.
   - Stick strictly to **C# 5 syntax** (no auto-property initializers, string interpolation `$"..."`, null-conditional `?.`, or `nameof`).
   - Do not introduce external NuGet dependencies. Use built-in BCL assemblies (`System.Web.Extensions` for `JavaScriptSerializer`) and P/Invoke (`XInput1_4.dll`, `xinput9_1_0.dll`, `winmm.dll`, `vJoyInterface.dll`).

2. **Design Philosophy (Pure Pass-Through)**:
   - Target games (such as *Wardogs*) provide their own in-game HOTAS curves, flight assists, and deadzones.
   - Keep `MappingEngine` linear and low-latency by default (`deadzone: 0.0`) unless the user explicitly asks for custom filtering or stateful axis modes (e.g., relative throttle hold).

3. **Preventing Input Feedback Loops**:
   - When using or extending `WinMMInputProvider` (`joyGetPosEx`), remember that `vJoy` itself registers as a WinMM joystick device. Avoid blindly auto-falling back from XInput to WinMM index 0 if vJoy is the only WinMM device, or you will feed vJoy's output back into its own input.

4. **vJoy Range & Conventions**:
   - `vJoyInterface.dll` is loaded dynamically from `C:\Program Files\vJoy\x64` via `SetDllDirectory`.
   - Standard vJoy axis range is `1` (`0x1`) to `32768` (`0x8000`) with center at `16384` (`0x4000`).
   - Continuous POV values are in hundredths of a degree (`0` to `35900`, `-1` for neutral).

5. **Headless Verification**:
   - Run `.\build.ps1` followed by `Start-Process .\GamepadToHotas.exe -ArgumentList "--self-test" -Wait` (which writes `selftest_result.txt`) to verify profile serialization, `MappingEngine` output, and `vJoyInterface.dll` connectivity without opening the WinForms GUI.
