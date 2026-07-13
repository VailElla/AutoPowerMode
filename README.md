[English](README.md) | [简体中文](README.zh-CN.md)

# AutoPowerMode

Current version: v1.3.3

AutoPowerMode is a Windows-only system tray application that switches Windows power plans automatically according to whether the current user is actively using the computer.

GitHub Releases provide a small Windows x64 framework-dependent package. Windows must have the .NET 8 Desktop Runtime installed. Because the application is not code-signed, Windows may show an “Unknown publisher” warning; continue only when you downloaded the package from this repository.

The application starts directly in the system tray. After the configured idle threshold it switches to the idle power plan, and it switches back to the active power plan when keyboard or mouse input resumes.

## Language

- On first launch, every Chinese Windows culture (`zh-*`) uses Simplified Chinese.
- Every non-Chinese Windows culture uses English.
- Open **Settings → Language** to choose **Follow system language**, **English**, or **Simplified Chinese** at any time.
- The selected preference is stored locally in the current user's configuration file.

## Features

- Runs in the system tray without opening a main window at startup.
- Uses Windows `GetLastInputInfo` to detect keyboard and mouse idle time.
- Uses a 1,200-second idle threshold by default. Active and idle-plan checks are independently configurable from 1–60 seconds, defaulting to 30 seconds while active or not yet classified and 1 second after entering the idle plan so returning input can restore the active plan quickly.
- Provides two independent, opt-in idle protections, both disabled by default: skip the idle rule while another program declares `ES_SYSTEM_REQUIRED`, `ES_DISPLAY_REQUIRED`, or `ES_AWAYMODE_REQUIRED`, and skip it while the foreground window is fullscreen.
- Calls `powercfg /setactive` only when the target state changes, then verifies the active plan GUID before reporting success.
- Provides concise notifications for startup synchronization, successful switches, failed switches, and external power-plan changes.
- Requires two consecutive idle checks before switching to the idle plan, while user activity resumes the active plan immediately.
- Supports pause/resume, manual plan switching, external override protection, diagnostics, startup registration, and single-instance activation.
- Uses Per-Monitor V2 DPI layout from 100% through 250%, including dynamic relayout across displays, content-aware initial sizing, a stacked narrow-window layout with vertical-only scrolling, and an always-visible bottom action bar.
- Stores configuration and rotating logs locally under the current user's AppData directory.

## Privacy

AutoPowerMode contains no telemetry, analytics, advertising SDK, remote API client, update beacon, or background upload code. It does not send configuration, power-plan details, diagnostics, logs, user names, file paths, or device information to this project or any third party.

All normal processing is local: Windows idle detection, system execution-state reads, foreground-window and monitor-bound comparisons, `powercfg`, current-user startup registration, configuration, diagnostics, and logs. The only feature that can open a network destination is the user-initiated **GitHub project page** button, which asks Windows to open this public repository in the default browser. Copying diagnostics is also user-initiated and writes only to the local clipboard.

Logs are bounded by rotation and sanitize the AppData application path and executable directory. Release builds disable debug symbols, and GitHub Actions builds release archives from source without local build artifacts.

## Project structure

```text
AutoPowerMode.sln               Visual Studio / dotnet solution
src/AutoPowerMode/              Application source
  Configuration/               Configuration model and persistence
  Localization/                System-language detection and UI strings
  Models/                       State and data models
  Policies/                     Switching and notification policies
  Services/                     Windows and local-system services
  UI/                           Tray, settings, and diagnostics UI
tests/AutoPowerMode.Tests/      Logic tests
tests/AutoPowerMode.WindowsUi.Tests/ Windows-only Settings UI smoke test
docs/reviews/                   Historical release reviews
archive/                        Git-ignored local build history
```

## Build and test

```bash
dotnet build AutoPowerMode.sln --configuration Release
dotnet run --project tests/AutoPowerMode.Tests/AutoPowerMode.Tests.csproj
# Windows only:
dotnet run --project tests/AutoPowerMode.WindowsUi.Tests/AutoPowerMode.WindowsUi.Tests.csproj
```

The logic tests cover configuration migration, independently configurable dual-rate monitoring, idle protections, language selection and persistence, power-plan parsing, switch policies, notifications, DPI layout, log rotation and path sanitization, startup registration, and external override protection. The Windows-only smoke test opens the localized Settings form and exercises both wide and compact layouts.

## License and contributing

AutoPowerMode is open source software released under the [MIT License](LICENSE).
Contributions, bug reports, and focused pull requests are welcome. See
[CONTRIBUTING.md](CONTRIBUTING.md) for the development and validation workflow.
For security vulnerabilities, follow [SECURITY.md](SECURITY.md) instead of
opening a public issue.
