# GMK Movement Mapper

This is a clean-room replacement for the old GMK DriverNet user-mode app. It is deliberately split into three pieces:

1. A direct USB reader matching the original driver's interface 0, endpoint `0x81`, and 13-byte GMK report format.
2. A choice between eight-way keyboard movement and native analog movement through an on-demand virtual Xbox controller.
3. An optional HidHide integration that invokes the installed HidHideCLI. HidHide remains an installed, signed kernel filter; it is not copied into this application or replaced with an unsigned driver.

The movement keys default to W/A/S/D and the GMK A button defaults to comma. Click any displayed key and press a new keyboard key to remap it. The live stick panel shows the physical input vector, deadzone, selected eight-way output, and A-button state.

The public GMK source confirms that the original app used VID `0483`, joystick PIDs `5750`/`5751`, LibUsbDotNet, and ViGEm.NET. Keyboard mode creates no virtual controller. Controller mode creates one only while **Start movement** is active and removes it immediately when movement stops.

## Installation

1. **Download** `GMKMovementMapper.exe` from the [Releases page](https://github.com/thedawnpirate1-spec/GMK-Movement-Mapper/releases). It is a single, self-contained Windows x64 executable — no installer, no separate .NET runtime, no Visual Studio.
2. **Put it anywhere** you like (Desktop, a Programs folder, wherever). Nothing needs to be extracted or installed alongside it.
3. **First launch — Windows SmartScreen warning is expected.** Because this is an independent, unsigned build, Windows shows *"Windows protected your PC"* the first time you run it. Click **More info**, then **Run anyway**. This is normal for small open-source tools without a paid code-signing certificate; it is not a sign of a virus, and you can verify that yourself by building from source (see below) or reading the code in `Source/`.
4. **(Optional) Install [HidHide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/)** if you want the app to hide the physical GMK from games like Fortnite so only the mapped keyboard/controller input is seen. Not required for basic movement mapping to work.
5. Run the app and follow **First run** below.

### Building from source instead

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
git clone https://github.com/thedawnpirate1-spec/GMK-Movement-Mapper.git
cd GMK-Movement-Mapper/Source/GMKMovementMapper
dotnet publish GMKMovementMapper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The finished executable is written to `bin\Release\net9.0-windows\win-x64\publish\GMKMovementMapper.exe`.

## First run

1. Leave GMK DriverNet and AntiMicro closed.
2. Start this mapper and click **Set up HidHide**. The mapper adds itself to HidHide's allow-list and hides only the connected GMK device.
3. Click **Start movement**, then start Fortnite. HidHide changes normally take effect immediately; reconnecting or reopening is not required.

The interface scales with Windows display scaling and can be resized. Key choices and deadzone changes save automatically.
Use **Dark mode** in the header to switch themes. The choice is remembered. **Help** beside the HidHide button opens a short five-step setup guide.

Version 16 adds a true tray performance state. Minimizing suspends live drawing, dashboard updates and diagnostics while the full-speed USB, mapping, output and reconnect pipeline continues; restoring resumes the same interface and running state. It also replaces the remaining native bright tab strip with a custom themed control, darkens status surfaces, improves dashboard consistency and uses the GMK Mapper name throughout the window and taskbar.

## Movement modes

**Keyboard movement** is the safe default while Fortnite's mixed mouse/controller input bug exists. It creates no virtual controller and outputs the selected four movement keys.

**Controller movement** restores native analog movement through a virtual Xbox 360 controller. The controller is connected only after **Start movement** is clicked and is disconnected immediately by **Stop movement**, closing the app, or an output failure. Controller mode requires the signed ViGEmBus driver used by the original GMK software.

Controller profiles support a configurable diagonal target from 0° to 90°, measured outward from forward. The start and width of both forward-only activation zones can be adjusted independently; the zones are drawn in purple on the live display. The default target is 71°; 76° reproduces the older GMK preset. Profiles also store stick rotation, inner and outer deadzones, anti-deadzone, a custom response curve, and the virtual Xbox button produced by the physical GMK A button.

The profile is stored at `%APPDATA%\\GMKRebuild\\profile.json`. `DiagonalSnapDegrees` and `DiagonalBiasDegrees` are intentionally profile values so the diagonal behaviour can be tuned later without changing code.

## Fortnite settings for joystick-like keyboard movement

Keyboard mode splits the stick into **eight equal 45° sectors** (W, W+A, A, S+A, S, …). For the stick to feel like a real joystick, Fortnite must move in the *centre* of each sector for the matching key combination. Set this in Fortnite under **Keyboard Controls → Keyboard Movement** (DE: *Tastatur-Bewegung*):

| Fortnite setting (DE) | Recommended |
|---|---|
| Use Custom Diagonals (*Angepasste Diagonalen verwenden*) | On (Off also gives 45/90/135) |
| Forward Diagonal Angle (*Vorwärts-Diagonal-Winkel*) | **45** |
| Sideways Angle (*Seitwärts-Winkel*) | **90** |
| Backward Diagonal Angle (*Rückwärts-Diagonal-Winkel*) | **135** |

Why: with a sideways angle of 71, pushing the stick fully left still moves you 19° forward; a backward angle of 121 makes back-diagonals 14° flatter than the stick. 45/90/135 gives equal steps between all eight directions, so the maximum error versus the physical stick is an even ±22.5°.

Notes:

- Keyboard mode can only produce eight directions. For truly stepless movement use **Controller movement**.
- The 71° value belongs to the controller-mode **diagonal lock** (`ControllerDiagonalAngle`), not to Fortnite's keyboard sideways angle.
- A 71° sideways angle for strafing is a deliberate play-style choice, not joystick-accurate movement.

## Important limitation

The mapper reads the same native USB reports as GMK DriverNet. The original GMK Driver must remain closed so only one program owns the USB interface.

## GMK not detected?

Some GMK hardware revisions don't expose the native USB interface this app looks for by default — see [XBOX_FALLBACK_GUIDE.md](XBOX_FALLBACK_GUIDE.md) for how to tell which revision you have and what to do if detection still fails after updating.

See RESEARCH.md for the original-driver findings and the performance changes applied in v17.
