# AutoPowerMode

当前版本：v1.0.8

AutoPowerMode 是一个 Windows-only 桌面托盘程序，用于根据当前用户是否正在使用电脑，自动切换 Windows 电源计划。

## 直接下载 Windows 版

不会编程也可以直接使用：

[下载 AutoPowerMode Windows x64 版](https://github.com/VailElla/AutoPowerMode/releases/latest/download/AutoPowerMode-v1.0.8-win-x64-self-contained.zip)

使用方法：

1. 点击上面的下载链接。
2. 下载完成后解压 zip 文件。
3. 双击 `AutoPowerMode.exe` 运行。

默认发布包不内置 .NET 运行时，体积更小。运行前需要 Windows 已安装 .NET 8 Desktop Runtime；如果没有安装，启动时会由 .NET 提示缺少运行时，按提示自行安装即可。因为软件目前没有购买代码签名证书，Windows 可能提示“未知发布者”；如果你确认是从本仓库下载，可以在提示中选择“更多信息”后继续运行。

程序启动后不会显示主窗口，而是常驻系统托盘。用户空闲达到阈值后切换到节能计划，重新使用鼠标或键盘后切换到高性能计划。

## 功能列表

- 启动后直接进入系统托盘
- 使用 `GetLastInputInfo` 检测鼠标和键盘空闲时间
- 默认空闲 1200 秒后切换到节能计划
- 默认每 10 秒检测一次
- 状态变化时才调用 `powercfg /setactive`
- 支持 10 秒内部切换冷却时间
- 支持暂停和恢复自动切换
- 支持托盘菜单手动切换到高性能或节能计划
- 支持设置空闲阈值、检测间隔、活跃计划、空闲计划
- 支持当前用户级别开机自启
- 支持单实例运行，再次启动 exe 会打开已有实例的设置窗口
- 设置会长期保存到当前用户 AppData，关闭后再次启动继续使用上次设置
- 配置损坏时自动备份并恢复默认配置
- 写入本地日志，方便排查问题

## 面向开发者

如果你只是想使用软件，请看上面的“直接下载 Windows 版”。下面内容只给需要自己构建的人使用。

### 构建方式

```powershell
dotnet build
```

### 运行方式

```powershell
dotnet run
```

### 发布方式

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

发布后运行生成目录中的 `AutoPowerMode.exe`。

这个发布方式是 framework-dependent，不内置 .NET 8 Desktop Runtime。若目标机器未安装运行时，启动时会提示用户安装。

程序只允许当前用户启动一个实例。如果再次运行 `AutoPowerMode.exe`，新的进程会通知已运行实例打开设置窗口，然后自动退出。

## 配置文件位置

```text
%AppData%\AutoPowerMode\config.json
```

配置示例：

```json
{
  "idleThresholdSeconds": 1200,
  "checkIntervalSeconds": 10,
  "idlePowerPlanGuid": "",
  "activePowerPlanGuid": "",
  "autoStart": true,
  "isPaused": false,
  "powerPlansConfiguredByUser": false
}
```

保存设置时会写入这个文件；重新启动或更换新版 exe 后，程序仍会从这里读取上次保存的设置。

空闲阈值使用秒，允许范围为 10 到 14400 秒，默认 1200 秒。
检测间隔使用秒，允许范围为 5 到 3600 秒，默认 10 秒。

## 日志文件位置

```text
%AppData%\AutoPowerMode\logs\app.log
```

程序也会同时尝试写入运行目录下的便携日志，方便排查启动失败：

```text
<程序所在目录>\logs\app.log
```

## 常见问题排查

### 托盘图标不显示

Windows 可能把托盘图标折叠到了隐藏区域。点击任务栏右侧的上箭头，检查 AutoPowerMode 是否在隐藏图标中。

### 电源计划没有切换

先右键托盘图标，确认状态不是 `Paused` 或 `NotConfigured`。然后打开设置，确认已经选择了活跃时电源计划和空闲时电源计划。

### 开机自启无效

AutoPowerMode 使用当前用户注册表：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

键名为：

```text
AutoPowerMode
```

如果程序移动到了新的路径，请重新打开设置并保存一次，程序会写入新的完整路径。

### `powercfg` 执行失败

打开日志文件检查 `powercfg` 的错误信息。也可以在 PowerShell 中手动运行：

```powershell
powercfg /list
powercfg /getactivescheme
```

如果这些命令本身失败，需要先修复 Windows 电源计划或系统权限问题。

### 找不到节能或高性能计划

程序会优先按名称匹配：

- `Power Saver` / `节能`
- `High Performance` / `高性能`

如果自动匹配失败，托盘菜单会显示 `NotConfigured`，请打开设置界面手动选择对应计划。
