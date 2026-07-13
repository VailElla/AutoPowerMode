# Contributing to AutoPowerMode

Thanks for helping improve AutoPowerMode. Contributions are welcome through
GitHub issues and pull requests.

## Before you start

- Search existing issues and pull requests before opening a new one.
- Do not report security vulnerabilities in a public issue. Follow
  [SECURITY.md](SECURITY.md) instead.
- Remove personal paths, logs, configuration files, and other sensitive data
  from diagnostic output before sharing it.

## Development environment

- .NET 8 SDK
- Windows 10 or later for running the application and the Windows UI smoke
  test
- A non-Windows development machine can build the Windows-targeted solution
  and run the logic tests, but cannot run the Windows UI smoke test locally.

Useful commands from the repository root:

```bash
dotnet restore AutoPowerMode.sln
dotnet format AutoPowerMode.sln --verify-no-changes
dotnet build AutoPowerMode.sln --configuration Release
dotnet run --project tests/AutoPowerMode.Tests/AutoPowerMode.Tests.csproj --configuration Release
# Windows only:
dotnet run --project tests/AutoPowerMode.WindowsUi.Tests/AutoPowerMode.WindowsUi.Tests.csproj --configuration Release
```

## Pull requests

- Keep each pull request focused on one coherent change.
- Add or update tests for behavior changes.
- Update `README.md`, `README.zh-CN.md`, or `CHANGELOG.md` when user-facing
  behavior changes.
- Explain the validation you ran and call out any Windows-only validation you
  could not perform.
- Do not commit `bin/`, `obj/`, `release/`, `publish/`, `dist/`, PDB files,
  logs, local configuration, or secrets.

GitHub Actions runs formatting, logic tests, and the Windows Settings UI smoke
test for pull requests.
