# GamepadToHOTAS

Lightweight, zero-dependency C# application that bridges a gamepad (such as **GameSir T3 Lite**) to a **vJoy Virtual HOTAS** device in **Helicopter Mode 2** (Left Stick: Collective/Yaw, Right Stick: Cyclic Pitch/Roll).

## Architecture (Data vs. Logic Separation)

- **`profiles/*.json`** — Declarative controller and mapping configurations. Anyone (or any AI agent) can add support for a new controller or flight mode by adding or editing a JSON file without recompiling.
- **[`src/ProfileSchema.cs`](src/ProfileSchema.cs)** — Data models and JSON serializer matching `profiles/*.json`. Read the doc comments at the top of this file for all supported axis and button identifiers.
- **[`src/InputProviders.cs`](src/InputProviders.cs)** — Hardware polling backends (`XInputProvider` for XInput controllers, `WinMMInputProvider` for DirectInput/WinMM controllers) producing a `NormalizedControllerState`.
- **[`src/MappingEngine.cs`](src/MappingEngine.cs)** — Pure transformation engine (`NormalizedControllerState` + `ControllerProfile` ➔ `VJoyStateSnapshot`) plus **Solo Bind Pulse** generator for conflict-free in-game keybinding.
- **[`src/VJoyOutput.cs`](src/VJoyOutput.cs)** — P/Invoke wrapper for `C:\Program Files\vJoy\x64\vJoyInterface.dll` and automated device setup via `vJoyConfig.exe`.
- **[`src/MainForm.cs`](src/MainForm.cs)** — Real-time visualizer, profile switcher, axis inverter, and 3-second Solo Bind helper UI.

## Building

No external SDK or NuGet packages required. Uses the built-in Windows .NET Framework C# compiler:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```
