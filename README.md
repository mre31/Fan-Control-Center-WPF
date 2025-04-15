# Fan Control Center

Fan Control Center is a Windows application designed to control fan speeds on Dell/Alienware computers. It provides a simple and intuitive UI to adjust fan speeds, monitor temperatures, and create custom fan profiles. Unlike Alienware Control Center, it only controls the fans and does not modify CPU and GPU power limits.

![Fan Control Center Home](https://github.com/user-attachments/assets/d11cfff8-0c5d-47ae-92b5-5462210be591)


## Features

- **Fan Speed Control**: Manually adjust CPU and GPU fan speeds
- **Temperature Monitoring**: Real-time monitoring of CPU and GPU temperatures
- **Multiple Profiles**: Includes predefined profiles (Silent, Balanced, Performance, G-Mode) and custom profiles
- **Hotkey Support**: Assign keyboard shortcuts to quickly switch between profiles
- **System Tray Integration**: Minimize to system tray for unobtrusive operation
- **Auto-start Option**: Launch automatically with Windows

## System Requirements

- Windows 10/11
- Dell/Alienware computer with AWCC support
- Administrator privileges (required to access AWCC controls)

## Installation

1. Download the latest release from the [releases page](https://github.com/mre31/Fan-Control-Center-WPF)
2. Extract the ZIP file to a location of your choice
3. Run `CFanControl.exe`
4. The application will request administrator privileges as they are required to control fan speeds

## Usage

### Profiles

- **Silent**: Minimal fan speed for quiet operation
- **Balanced**: Default profile that balances performance and noise
- **Performance**: Higher fan speeds for better cooling during demanding tasks
- **G-Mode**: Maximum cooling performance for gaming
- **Custom**: User-defined fan speed settings

### Assigning Hotkeys

1. Select a profile
2. Click "Change Hotkey"
3. Press the desired key combination
4. The hotkey will be assigned to the selected profile

## Compatibility

This application is not guaranteed to work on all Dell/Alienware laptop models. Please open an issue to report whether it works on your device or not.

### Known Compatible Models

- Dell G15 5530 (i7 13650HX/RTX4060)

## Building from Source

### Prerequisites

- Visual Studio 2019 or newer
- .NET Framework 4.7.2 or newer
- Windows SDK

### Build Steps

1. Clone the repository
2. Open the solution file in Visual Studio
3. Restore NuGet packages
4. Build the solution

## License

[MIT License](LICENSE)

## Acknowledgements

- This project is designed for Dell/Alienware computers and interfaces with the Alienware Command Center

---

*Note: This software modifies your system's fan control settings. Use at your own risk. The authors are not responsible for any damage caused by improper use of this software.*
