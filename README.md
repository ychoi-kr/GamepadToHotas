# GamepadToHOTAS

**GamepadToHOTAS** bridges a standard gamepad (such as the **GameSir T3 Lite** or Xbox controller) to a **vJoy Virtual HOTAS** device, allowing you to fly helicopters in **Mode 2** (Left Stick: Collective/Yaw, Right Stick: Cyclic Pitch/Roll) in games like *Wardogs* while keeping all gamepad buttons right under your fingers.

Instead of installing and configuring multiple heavy remapping tools, GamepadToHOTAS provides a single lightweight executable (~30 KB) with zero external runtime dependencies.

---

## Key Features

- **Helicopter Mode 2 Out of the Box**: Maps Right Stick to Cyclic Roll (`X`) & Pitch (`Y`), Left Stick X to Yaw/Rudder (`Rz`), Left Stick Y to Proportional PWM Collective Up/Down Buttons (`Btn #15` / `Btn #14`), Analog Triggers to `Rx`/`Ry` (centered at 50% neutral), D-Pad to Continuous POV Hat, and 16 buttons.
- **No Unwanted Auto-Takeoff or UI Cursor Drift**: By mapping Left Stick Up/Down to analog-proportional PWM buttons (`Btn #15` / `Btn #14`) while keeping all unused axes at 50% neutral, helicopters stay grounded on boarding (so you can use the Supply Purchase menu in *Wardogs* without cursor drift) while retaining smooth, proportional climb/descent control in flight.
- **Clean 1:1 Pass-Through**: Sends uncorrupted 16-bit axis signals at 250Hz+ so your game's native HOTAS settings (curves, deadzones, flight assists) work without gamepad auto-leveling oscillations (PIO).
- **One-Click vJoy Auto-Configuration**: Automatically configures vJoy Device #1 (8 Axes, 16 Buttons, 1 Continuous POV Hat) directly from the app—no need to open `vJoyConf`.
- **Directional 3-Second "Solo Bind" Helper**: Prevents the common issue where games detect your physical gamepad input before the virtual vJoy input during keybinding. Click **[Bind - (Dn)]** / **[Bind + (Up)]** next to an axis (or click any button cell) in the app, click the control slot in-game, and the app emits only the virtual HOTAS signal.
- **Instant Pause Toggle (`Back + Start`)**: Press `Back + Start` on your gamepad simultaneously (with audio beep feedback) to pause virtual HOTAS output.
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

### 3. Recommended In-Game Setup (*Wardogs*)
1. Go to **Settings > Gamepad > HOTAS** (`설정 > 게임패드 > HOTAS`) and enable **HOTAS**.
2. **Bind Cyclic & Yaw Axes** (using the 3-second Solo Bind buttons in **GamepadToHOTAS**):
   - **Roll Left / Right (`롤 왼쪽 / 오른쪽`)**: Click **[Bind - (Dn)]** / **[Bind + (Up)]** on `Cyclic Roll (Right Stick X -> vJoy X)`, then click the slot in-game within 3 seconds.
   - **Pitch Down / Up (`피치 내리기 / 올리기`)**: Click **[Bind - (Dn)]** / **[Bind + (Up)]** on `Cyclic Pitch (Right Stick Y -> vJoy Y)`, then click the slot in-game.
   - **Yaw Left / Right (`요 왼쪽 / 오른쪽`)**: Click **[Bind - (Dn)]** / **[Bind + (Up)]** on `Yaw / Rudder (Left Stick X -> vJoy Rz)`, then click the slot in-game.
3. **Bind Collective Up / Down as Buttons** (prevents auto-takeoff and Supply Purchase UI cursor drift):
   - **Collective Up (`콜렉티브 올리기`)**: Click **`Btn #15: Collective Up (LS Up PWM)`** in the bottom grid of **GamepadToHOTAS**, then click `콜렉티브 올리기` in-game within 3 seconds.
   - **Collective Down (`콜렉티브 내리기`)**: Click **`Btn #14: Collective Down (LS Down PWM)`** in **GamepadToHOTAS**, then click `콜렉티브 내리기` in-game within 3 seconds.
   - *(Note: Wardogs' Unreal Engine HOTAS input recognizes up to Button 15, so Collective Down/Up are placed on Buttons #14 and #15.)*
4. **Essential HOTAS Options in *Wardogs***:
   - **Auto-Adjust Collective (`자동 조정 콜렉티브`)**: Set to **Disabled (`비활성화됨`)**. When disabled alongside the button bindings above, the collective automatically settles to `0` upon boarding so the helicopter stays grounded while you buy supplies.
   - **Pitch Sensitivity (`피치 감도`)**: Recommended around **`0.60`** for smooth cyclic pitch control.

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
