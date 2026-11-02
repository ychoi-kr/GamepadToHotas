# GamepadToHOTAS

**GamepadToHOTAS** bridges a standard gamepad (such as the **GameSir T3 Lite** or Xbox controller) to a **vJoy Virtual HOTAS** device, allowing you to fly helicopters in **Mode 2** (Left Stick: Collective/Yaw, Right Stick: Cyclic Pitch/Roll) in games like *Wardogs* while keeping all gamepad buttons right under your fingers.

Instead of installing and configuring multiple heavy remapping tools, GamepadToHOTAS provides a single lightweight executable (~30 KB) with zero external runtime dependencies.

---

## Key Features

- **Helicopter Mode 2 Out of the Box**: Maps Left Stick to Collective (Z) & Yaw/Rudder (Rz), Right Stick to Cyclic Roll (X) & Pitch (Y), Analog Triggers to Rx/Ry (or combined Rudder), D-Pad to Continuous POV Hat, and 16 buttons.
- **Clean 1:1 Pass-Through**: Sends uncorrupted 16-bit axis signals at 250Hz+ so your game's native HOTAS settings (curves, deadzones, flight assists) work without double-filtering conflicts.
- **One-Click vJoy Auto-Configuration**: Automatically configures vJoy Device #1 (8 Axes, 16 Buttons, 1 Continuous POV Hat) directly from the app—no need to open `vJoyConf`.
- **3-Second "Solo Bind" Helper**: Prevents the common issue where games detect your physical gamepad input before the virtual vJoy input during keybinding. Click **[Bind (3s)]** in the app, click the control slot in-game, and the app emits only the virtual HOTAS signal.
- **Instant Pause Toggle (`Back + Start`)**: Press `Back + Start` on your gamepad simultaneously (with audio beep feedback) to pause virtual HOTAS output when exiting a helicopter to play on foot.
- **Data-Driven JSON Profiles**: All controller mappings live in `profiles/*.json`. Add or tweak controller layouts without recompiling.

---

## Installation & Quick Start

### 1. Prerequisite: Install vJoy Driver
Install the Windows 10/11 compatible fork of **vJoy**:
- Download and run the latest installer from **[BrunnerInnovation/vJoy Releases](https://github.com/BrunnerInnovation/vJoy/releases)**.

### 2. Download & Run GamepadToHOTAS
1. Go to the **[Releases](https://github.com/ychoi-kr/GamepadToHotas/releases)** page and download **`GamepadToHotas-win-x64.zip`** (or `GamepadToHotas.exe`).
2. Extract and run **`GamepadToHotas.exe`**.
3. If the status bar shows yellow (`⚡ Upgrade vJoy #1 to 16 Btn + POV`), click that button once to automatically configure vJoy Device #1.

### 3. Binding Controls in Game (e.g., Wardogs)
1. Enable **HOTAS Support** in your game's settings.
2. To bind an axis or button without triggering a physical gamepad conflict:
   - Click **[Bind (3s)]** next to the axis (or click the button cell) in **GamepadToHOTAS**.
   - Within 3 seconds, click the target binding slot in the game's settings menu.
   - Wait for the pulse to register, without touching your physical controller.

---

## Customizing Profiles

Profiles are stored as plain JSON files in the `profiles/` folder next to `GamepadToHotas.exe`.
- Click **Open Folder** in the app to view or edit `profiles/*.json`.
- Click **Reload JSON** to apply changes immediately without restarting.
- Use an AI coding assistant? Point it at the repository—`AGENTS.md` and `src/ProfileSchema.cs` document the full schema and extension points.

---

## Building from Source

No external SDKs, Visual Studio installation, or NuGet packages are required. The project compiles directly using the built-in Windows .NET Framework C# compiler (`csc.exe`):

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

---

## License

Released under the [MIT License](LICENSE).
