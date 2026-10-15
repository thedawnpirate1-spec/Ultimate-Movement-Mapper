# Findings and implementation choice

## Existing GMK build

The public repository [TannerHollis/GMKDriverNet](https://github.com/TannerHollis/GMKDriverNet) is the source that corresponds to the installed `GMKDriverNetUI.exe` family. Its driver code identifies GMK VID `0x0483` and joystick PIDs `0x5750` and `0x5751`, scans with `LibUsbDotNet.LibUsb`, and creates its output through `Nefarius.ViGEm.Client`. The published version string in `GMKDriver.cs` is `v0.95.7`.

That architecture explains the Fortnite behaviour: the app owns the physical USB joystick and publishes an Xbox 360/XInput device. Fortnite can see the virtual controller while the mouse is also active, so its input-method switching logic is involved. The old app also polls USB device counts and starts one worker thread per GMK device, which is more fragile than a single bounded polling loop.

## Rebuild choice

The replacement in this folder intentionally does not create a ViGEm controller. It reads the joystick through Windows' joystick API and emits only W/A/S/D with `SendInput`. That gives Fortnite one movement input family (keyboard) and leaves the mouse independent.

The mapper is separated from the device reader. If a GMK firmware revision is not exposed by the Windows joystick API, only `WinmmJoystickSource` needs to be replaced by a Raw Input/HIDP reader; the eight-way and keyboard code remains unchanged.

## Diagonal model

The mapper uses eight sectors. `DiagonalSnapDegrees` controls how close the stick must be to a diagonal before the diagonal wins. `DiagonalBiasDegrees` shifts the four diagonal centers, allowing the behaviour to be tuned later without changing code. `Balanced` gives the usual 22.5-degree split; `CardinalFirst`, `DiagonalFirst`, and `Custom` are available for testing.

## HidHide boundary

HidHide is a signed kernel-mode filter, so it should not be copied into an application or replaced by a newly built unsigned filter. The app invokes the installed `HidHideCLI.exe` to add its own executable to the allow-list, block the current GMK instance, and enable hiding. This uses the documented HidHide control surface. The installed HidHide driver still runs independently and does not require the configuration window to remain open.

References:

- [GMKDriverNet source](https://github.com/TannerHollis/GMKDriverNet)
- [HidHide API documentation](https://docs.nefarius.at/projects/HidHide/API-Documentation/)
- [HidHide CLI/API documentation](https://docs.nefarius.at/projects/HidHide/API-Documentation/)
- [HidHide simple setup guide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/)
