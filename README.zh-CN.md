[English](README.md) | [简体中文](README.zh-CN.md)

# AutoPowerMode

当前版本：v1.3.3

AutoPowerMode 是一个 Windows-only 桌面托盘程序，用于根据当前用户是否正在使用电脑，自动切换 Windows 电源计划。

GitHub Release 默认提供小体积的 Windows x64 framework-dependent 发布包，运行前需要 Windows 已安装 .NET 8 Desktop Runtime。因为软件目前没有购买代码签名证书，Windows 可能提示“未知发布者”；请仅在确认安装包来自本仓库时继续运行。

程序启动后不会显示主窗口，而是常驻系统托盘。用户空闲达到阈值后切换到空闲电源计划，重新使用鼠标或键盘后切换回活跃电源计划。

## 语言

- 首次启动时，所有中文 Windows 系统文化（`zh-*`）使用简体中文。
- 中文以外的系统文化统一使用英文。
- 可以随时打开 **设置 → 语言**，主动选择 **跟随系统语言**、**English** 或 **简体中文**。
- 语言偏好仅保存在当前用户的本地配置文件中。

## 功能列表

- 启动后直接进入系统托盘，不自动打开主窗口。
- 使用 Windows `GetLastInputInfo` 检测鼠标和键盘空闲时间。
- 默认空闲阈值为 1200 秒；活跃与空闲计划的检测频率都可在 1–60 秒内独立设置，默认活跃或尚未判定状态每 30 秒检测一次，进入空闲计划后每 1 秒检测一次，以便用户回来时快速恢复活跃计划。
- 提供两个默认关闭、可独立勾选的空闲误触保护：其他程序声明 `ES_SYSTEM_REQUIRED`、`ES_DISPLAY_REQUIRED` 或 `ES_AWAYMODE_REQUIRED` 时不应用空闲规则；当前前台窗口为全屏应用时不应用空闲规则。
- 只在目标状态变化时调用 `powercfg /setactive`，并在报告成功前复核当前计划 GUID。
- 支持简短系统通知，区分启动同步、切换成功、切换失败和外部电源计划改动。
- 活跃切换到空闲前需要连续 2 次检测确认；检测到用户输入后立即恢复活跃计划。
- 支持暂停/恢复、手动切换、外部覆盖保护、诊断信息、当前用户开机自启和单实例运行。
- 设置页使用 Per-Monitor V2 DPI，支持 100%–250% 缩放和跨显示器动态重排；初始窗口会按内容展开，窄窗口自动改为上下排列且只纵向滚动，底部操作按钮始终保留显示。
- 配置和轮转日志仅保存在当前用户 AppData 目录。

## 隐私

AutoPowerMode 不包含遥测、分析、广告 SDK、远程 API 客户端、更新信标或后台上传代码。软件不会向本项目或任何第三方发送配置、电源计划详情、诊断信息、日志、用户名、文件路径或设备信息。

所有正常处理均在本机完成，包括 Windows 空闲检测、系统执行状态读取、前台窗口与显示器边界比较、`powercfg`、当前用户开机自启注册、配置、诊断和日志。唯一可能打开网络地址的功能是用户主动点击 **GitHub 项目主页** 按钮，此操作只会请求 Windows 使用默认浏览器打开本公开仓库。复制诊断信息也必须由用户主动触发，并且只写入本机剪贴板。

日志有大小与数量上限，并会脱敏 AppData 应用路径和程序目录。Release 构建禁用调试符号，GitHub Actions 只从源码构建发布包，不会打包本机历史产物。

## 项目结构

```text
AutoPowerMode.sln               Visual Studio / dotnet 解决方案
src/AutoPowerMode/              应用源码
  Configuration/               配置模型与持久化
  Localization/                系统语言判断与界面文本
  Models/                       状态和数据模型
  Policies/                     切换与通知判定规则
  Services/                     Windows 和本地系统服务
  UI/                           托盘、设置和诊断界面
tests/AutoPowerMode.Tests/      纯逻辑自动测试
tests/AutoPowerMode.WindowsUi.Tests/ Windows 专用设置窗口冒烟测试
docs/reviews/                   历史版本审查记录
archive/                        被 Git 忽略的本地构建历史
```

## 构建与测试

```bash
dotnet build AutoPowerMode.sln --configuration Release
dotnet run --project tests/AutoPowerMode.Tests/AutoPowerMode.Tests.csproj
# 仅限 Windows：
dotnet run --project tests/AutoPowerMode.WindowsUi.Tests/AutoPowerMode.WindowsUi.Tests.csproj
```

逻辑测试覆盖配置迁移、可独立配置的双频检测策略、空闲误触保护、语言选择与持久化、电源计划解析、切换策略、通知、DPI 布局、日志轮转与路径脱敏、开机自启和外部覆盖保护。Windows 专用冒烟测试会实际打开中英文设置窗口并验证宽窄布局切换。
