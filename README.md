# HotCornersWin
A lightweight Windows hot corners app with full multi-monitor support.
Built from scratch in C# as a modern alternative to classic hot corner utilities. inspire from winxcorner.
<img width="1199" height="965" alt="photo_2026-06-12_07-16-08" src="https://github.com/user-attachments/assets/6c28a131-ef61-4f1c-9187-9c899e047e00" />



## Features
- ✅ Multi-monitor support with per-monitor corner configuration
- ✅ 10 built-in actions per corner
- ✅ Custom keyboard shortcuts per corner
- ✅ Volume control with configurable percentage step
- ✅ Dark/Light theme following Windows accent color
- ✅ Portable - settings stored next to exe
- ✅ Start with Windows option
- ✅ DPI-aware on all display resolutions
 <img width="1108" height="799" alt="photo_2026-06-12_07-16-13" src="https://github.com/user-attachments/assets/5e726f0e-dab7-4cd5-8b8f-fec020724a4c" />



## Requirements

- .NET Framework 4.6.1
- Windows 7 / 8.1 / 10 / 11 (x86, x64, ARM32, ARM64)

### Platform Notes

| Platform | Status | Notes |
|---|---|---|
| Windows 11 x64/ARM64 | ✅ Fully supported | |
| Windows 10 x64/ARM64 | ✅ Fully supported | |
| Windows 10 ARM32 | ✅ Fully supported | |
| Windows 8.1 x86/x64 | ✅ Supported | |
| Windows 8.1 ARM (RT) | ✅ Supported | Install [KB4486105](https://go.microsoft.com/fwlink/?linkid=2088632) first to get .NET 4.7.2 |
| Windows 7 | ⚠️ Partial | Install .NET 4.7.2 manually, DPI scaling may not work | not tested yet.

### Windows RT 8.1 Setup
1. Download and install `windows8.1-kb4486105-arm.msu`
2. your device must be jailbrokern
3. No need to restart but restart if it dont work on your device
4. Run `HotCornersWin.exe` normally

## Usage
1. Run HotCornersWin.exe
2. Right-click or double-click the tray icon to open Settings
3. Assign actions to corners per monitor
4. Click OK to save

## Built with
- C# .NET Framework 4.6.1
- MaterialSkin.2 for modern UI
- Built with assistance from Claude (Anthropic) - https://claude.ai
