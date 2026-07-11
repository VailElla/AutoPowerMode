# AutoPowerMode v1.1.2 功能审查说明

本文档用于交给其他智能体或审查者，对 AutoPowerMode v1.1.2 做功能、代码安全、隐私、可靠性、测试和后续优化审查。内容基于当前仓库源码和已发布的 v1.1.2 结果重新核验，不依赖聊天摘要。

## 1. 审查对象

- 项目名：AutoPowerMode
- 当前应用版本：`v1.1.2`
- 应用类型：Windows-only WinForms 桌面托盘程序
- 目标框架：`.NET 8`，`net8.0-windows`
- 发布形态：Windows x64 framework-dependent，不内置 .NET 运行时
- 运行前置条件：目标机器需要安装 `.NET 8 Desktop Runtime`
- 当前仓库远端：`https://github.com/VailElla/AutoPowerMode.git`
- 当前主分支提交：`3f32d20`，提交信息为 `Release AutoPowerMode v1.1.2`
- 当前 tag：`v1.1.2`
- GitHub Release：`https://github.com/VailElla/AutoPowerMode/releases/tag/v1.1.2`
- Release 状态：非 draft，非 prerelease
- 线上资产：
  - `AutoPowerMode-v1.1.2-win-x64-framework-dependent.zip`
  - `SHA256SUMS-v1.1.2.txt`
- Release zip SHA256：`2b31c3c83f6eef3431eb6c9bdf45efed3eef97cc831592247332f442b1e19447`

## 2. 产品目标

AutoPowerMode 的目标是根据当前 Windows 用户是否正在使用电脑，自动切换当前系统电源计划：

- 用户活跃时使用高性能电源计划。
- 用户空闲达到阈值并连续确认后使用节能电源计划。
- 用户重新移动鼠标或使用键盘时尽快恢复高性能电源计划。
- 程序常驻系统托盘，不显示主窗口。
- 用户可通过托盘菜单暂停、恢复、手动切换计划、打开设置、设置开机自启或退出。

该程序没有远程账号、云同步、网络更新、遥测、崩溃上报或后台服务。当前实现主要修改本机用户配置文件、本机日志、HKCU Run 注册表值以及 Windows 当前电源计划。

## 3. 仓库结构和模块职责

```text
AutoPowerMode/
  AppInfo.cs                         版本号和显示名
  AppConfig.cs                       配置模型、默认值和范围约束
  ConfigService.cs                   配置读写、旧配置迁移、损坏配置备份
  IdleDetector.cs                    Win32 GetLastInputInfo 空闲时间检测
  Logger.cs                          双位置日志、轮转、路径脱敏、失败吞掉
  PowerModeState.cs                  Active/Idle/Paused/NotConfigured 状态枚举
  PowerModeTransitionPolicy.cs       自动切换状态边界策略
  PowerPlan.cs                       电源计划数据模型和显示文本
  PowerPlanManager.cs                powercfg 调用、计划解析、自动匹配和切换
  PowerPlanOverridePolicy.cs         外部手动切换保护策略
  Program.cs                         程序入口、Windows 检查、异常处理、单实例启动
  SettingsForm.cs                    WinForms 设置窗口
  SingleInstanceService.cs           单实例互斥锁和本机命名管道激活
  StartupRegistrationValue.cs        HKCU Run 值解析、路径匹配、路径加引号
  StartupService.cs                  当前用户级别开机自启注册表读写
  AutoPowerMode.Tests/               纯逻辑测试项目
  AutoPowerMode.csproj               主项目配置和 Release 调试符号设置
  .github/workflows/release.yml      GitHub Actions 发布流程
  README.md                          用户说明
```

## 4. 启动流程

入口位于 `Program.Main()`。

启动步骤：

1. 如果当前系统不是 Windows，弹窗提示 `AutoPowerMode 只能在 Windows 上运行。` 后退出。
2. 初始化 WinForms：`ApplicationConfiguration.Initialize()`。
3. 注册 UI 线程异常处理：`Application.ThreadException`。
4. 注册 AppDomain 未处理异常记录：`AppDomain.CurrentDomain.UnhandledException`。
5. 创建 `SingleInstanceService`。
6. 如果不是第一个实例，尝试通过命名管道通知已有实例打开设置窗口，然后新进程退出。
7. 如果是第一个实例，创建 `TrayAppContext`。
8. 启动单实例激活服务器。
9. 调用 `Application.Run(trayAppContext)` 进入托盘程序生命周期。

启动失败时会写日志并弹窗提示日志位置。日志位置提示经过路径脱敏。

## 5. 系统托盘

程序启动后创建 `NotifyIcon`，图标为 `SystemIcons.Application`，不打开主窗口。

托盘菜单包含：

- 版本：显示 `AppInfo.Version`
- 当前状态：`Active`、`Idle`、`Paused` 或 `NotConfigured`
- 当前电源计划名称
- 空闲阈值，单位秒
- 检测间隔，单位秒
- 暂停或恢复自动切换
- 立即切换到高性能计划
- 立即切换到节能计划
- 设置
- 开机自启状态切换
- 退出

托盘图标双击会打开设置窗口。右键菜单打开时会重新读取当前活跃电源计划并刷新菜单显示。

托盘 tooltip 文本为：

```text
AutoPowerMode v1.1.2: <state>
```

如果 Windows 的 NotifyIcon 文本长度限制导致赋值异常，会退回 `AutoPowerMode v1.1.2`。

## 6. 自动检测循环

核心循环位于 `TrayAppContext.MonitorLoopAsync()`。

每轮流程：

1. 调用 `EvaluateOnceAsync()` 执行一次状态评估。
2. 读取当前配置快照。
3. 按 `CheckIntervalSeconds` 等待下一轮。
4. 如果后台循环异常，写日志，然后等待 1 分钟再继续。
5. 应用退出时通过 `CancellationToken` 停止循环。

默认检测间隔为 `10` 秒，可配置范围为 `5` 到 `3600` 秒。

## 7. 状态模型

`PowerModeState` 有四个状态：

| 状态 | 含义 |
| --- | --- |
| `Active` | 逻辑状态为用户活跃，目标应为活跃电源计划 |
| `Idle` | 逻辑状态为用户空闲，目标应为空闲电源计划 |
| `Paused` | 用户暂停自动切换 |
| `NotConfigured` | 缺少有效电源计划配置，无法自动切换 |

显示状态由 `GetDisplayState()` 决定：

- 配置 `IsPaused=true` 时显示 `Paused`。
- 配置无效时显示 `NotConfigured`。
- 配置有效且未暂停时显示内部 `_currentState`。

## 8. 状态边界策略

状态边界策略由 `PowerModeTransitionPolicy` 实现。

默认策略：

- `Active -> Idle`：需要 `idleTime >= idleThreshold` 连续 `2` 次检测确认。
- `Idle -> Active`：只要 `idleTime < activeResumeThreshold`，立即建议切回 `Active`。
- 当前实现传入的 `activeResumeThreshold` 等于 `idleThreshold`。
- 暂停或未配置时会清空连续空闲计数。
- 每次 `SetState()` 都会调用 `MarkState()`，同步状态并清空连续空闲计数。

这一策略解决的是阈值边界抖动问题。10 秒 cooldown 仍保留，但 cooldown 只是限制自动切到空闲计划的调用频率；连续确认才是状态稳定判断。

审查注意：

- `Idle -> Active` 不受 cooldown 限制，符合“用户一动鼠标就恢复高性能”的目标。
- `Active -> Idle` 需要两次检测。默认检测间隔 10 秒、默认空闲阈值 1200 秒，因此实际切到空闲计划通常发生在空闲阈值达到后的下一次确认。
- 如果用户把检测间隔调得很长，`Active -> Idle` 的确认延迟也会变长。

## 9. 空闲检测

`IdleDetector` 使用 Win32 API：

```text
user32.dll!GetLastInputInfo
```

计算逻辑：

1. 创建 `LASTINPUTINFO` 结构并设置结构大小。
2. 调用 `GetLastInputInfo` 获取最后一次输入 tick。
3. 使用 `Environment.TickCount` 当前值减去最后输入 tick。
4. 返回 `TimeSpan.FromMilliseconds(idleMilliseconds)`。

实现使用 `unchecked((uint)Environment.TickCount)`，能处理 32 位 tick 回绕场景。

检测范围是当前 Windows 会话的鼠标和键盘输入，不是应用内部 UI 事件。

## 10. 自动切换电源计划

自动切换由 `TrayAppContext.EvaluateOnceAsync()` 和 `SwitchToConfiguredPlanAsync()` 驱动。

自动评估流程：

1. 获取当前配置快照。
2. 检查配置是否暂停或未配置。
3. 暂停时进入 `Paused`，不调用空闲检测。
4. 未配置时进入 `NotConfigured`，不调用空闲检测。
5. 配置有效时调用 `IdleDetector.GetIdleTime()`。
6. 把空闲时间、阈值、暂停状态和配置状态交给 `PowerModeTransitionPolicy.Evaluate()`。
7. 如果策略返回 `Active` 或 `Idle`，执行电源计划切换。
8. 如果策略返回 `Paused` 或 `NotConfigured`，只更新状态，不切换计划。
9. 如果策略返回 `null`，本轮不做动作。

切换计划前会调用 `PowerPlanManager.GetActivePowerPlan()` 读取当前系统实际活跃计划。

自动切到空闲计划时还有 10 秒 cooldown：

```text
SwitchCooldown = 10 seconds
```

手动切换不受 cooldown 限制；自动切回活跃计划也不受 cooldown 限制。

## 11. 外部手动切换保护

`PowerPlanOverridePolicy` 用于防止程序抢回用户手动切换到的第三方或 OEM 电源计划。

规则：

- 如果是用户从 AutoPowerMode 托盘菜单手动触发切换，永远不跳过。
- 如果当前系统计划为空，无法判断外部覆盖，不跳过。
- 如果当前系统计划等于配置的活跃计划或空闲计划，不跳过。
- 如果当前系统计划既不是配置的活跃计划，也不是配置的空闲计划，则视为外部手动切换，自动切换跳过。

审查注意：

- 这能避免程序覆盖用户临时选中的 OEM 自定义计划。
- 当前策略不会弹窗提示，只写 Info 日志。
- 如果用户希望恢复自动控制，需要把当前计划切回已配置的活跃/空闲计划，或在设置中重新选择计划。

## 12. 手动切换

托盘菜单提供：

- 立即切换到高性能计划
- 立即切换到节能计划

手动切换调用同一套 `SwitchToConfiguredPlanAsync()` 和 `SwitchToPlanAsync()`，但 `manual=true`。

手动切换行为：

- 不受 10 秒 cooldown 限制。
- 不触发外部手动切换保护跳过。
- 如果当前已经是目标计划，只更新内部状态并跳过 `powercfg /setactive`。
- 如果切换成功，更新 `_lastSwitchTime`、当前电源计划和内部状态。

## 13. 电源计划管理

`PowerPlanManager` 通过 Windows 内置 `powercfg` 完成所有电源计划操作。

调用命令：

```text
powercfg /list
powercfg /getactivescheme
powercfg /setactive <guid>
```

进程启动设置：

- `FileName = "powercfg"`
- `UseShellExecute = false`
- `CreateNoWindow = true`
- 重定向 stdout/stderr
- `WaitForExit(10_000)`，超时 10 秒
- 超时后尝试 `Kill(entireProcessTree: true)`

`SetActivePlan()` 会先读取当前活跃计划，如果已经是目标 GUID，则跳过 `/setactive`。

## 14. powercfg 输出解析

解析逻辑位于 `PowerPlanManager.ParsePlans()`。

当前正则：

```text
(?i)\{?([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\}?\s+\((.*)\)\s*(\*)?\s*$
```

支持：

- 标准 GUID。
- GUID 外有无 `{}`。
- 括号中的计划名称。
- 计划名称包含括号，例如 `My Plan (Gaming)`。
- 行尾可选 `*` 表示当前活跃计划。
- 英文、简体中文、繁体中文等不同语言前缀，因为解析只依赖 GUID 和括号格式。

测试样本覆盖：

- English Windows：`Balanced`、`Power Saver`、`High Performance`
- 简体中文：`平衡`、`节能`、`高性能`
- 繁体中文：`平衡`、`節能`、`高效能`
- OEM 名称：例如 `ASUS Recommended`
- 特殊名称：`My Plan (Gaming)`
- 缺失标准高性能/节能计划时不凭空生成配置

审查注意：

- 解析仍假设 powercfg 输出中计划名位于最后一组括号中，且活跃标记为 `*`。
- 如果未来 Windows 改变输出格式，可能仍需更新解析器。

## 15. 自动匹配电源计划

`TryAutoConfigure()` 尝试自动补全无效或缺失的计划 GUID。

空闲计划候选：

- 名称精确或包含：`Power Saver`、`节能`、`節能`、`省电`、`省電`、`节电`、`節電`
- 或 GUID 为 Windows 标准 Power Saver GUID：`a1841308-3541-4fab-bc81-f71556f20b4a`

活跃计划候选：

- 名称精确或包含：`High Performance`、`高性能`、`高效能`
- 或 GUID 为 Windows 标准 High Performance GUID：`8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c`

平衡计划识别：

- GUID 为 `381b4222-f694-41f0-9685-ff5bb260df2e`
- 或名称为 `Balanced`、`平衡`、`平衡模式`

如果活跃计划被配置成平衡计划，并且能找到高性能计划，程序会自动修正为高性能计划。

审查注意：

- `PowerPlansConfiguredByUser` 会被保存，但当前 `TryAutoConfigure()` 没有用它阻止自动修正。
- 如果系统不存在高性能或节能计划，程序会进入 `NotConfigured`，或需要用户手动选择可用计划。
- 部分设备或 OEM 系统可能默认没有 High Performance 或 Power Saver。

## 16. 配置文件

配置目录：

```text
%AppData%\AutoPowerMode
```

配置文件：

```text
%AppData%\AutoPowerMode\config.json
```

当前写出的 JSON schema：

```json
{
  "idleThresholdSeconds": 1200,
  "checkIntervalSeconds": 10,
  "idlePowerPlanGuid": "",
  "activePowerPlanGuid": "",
  "autoStart": false,
  "isPaused": false,
  "powerPlansConfiguredByUser": false
}
```

默认值：

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `IdleThresholdSeconds` | `1200` | 空闲 20 分钟后准备切到节能计划 |
| `CheckIntervalSeconds` | `10` | 每 10 秒检测一次 |
| `IdlePowerPlanGuid` | 空字符串 | 未配置 |
| `ActivePowerPlanGuid` | 空字符串 | 未配置 |
| `AutoStart` | `false` | 默认不开机自启 |
| `IsPaused` | `false` | 默认不暂停 |
| `PowerPlansConfiguredByUser` | `false` | 默认不是用户手动配置 |

数值约束：

- 空闲阈值最小 `10` 秒，最大 `14400` 秒。
- 检测间隔最小 `5` 秒，最大 `3600` 秒。
- 小于等于 0 的秒字段会先回到默认值，再参与范围 clamp。

读配置时支持：

- JSON 注释
- 尾随逗号
- 旧字段 `idleThresholdMinutes`
- 旧字段 `checkIntervalMinutes`

旧字段迁移：

- 如果缺少 `idleThresholdSeconds` 但存在 `idleThresholdMinutes`，迁移为 `idleThresholdMinutes * 60`。
- 如果缺少 `checkIntervalSeconds` 但存在 `checkIntervalMinutes`，迁移为 `checkIntervalMinutes * 60`。
- 如果新旧字段同时存在，优先使用新秒字段。
- 迁移后仍会执行 Normalize 和范围 clamp。

写配置策略：

- 先写临时文件 `config.<processId>.<guid>.tmp`。
- 如果目标配置存在，用 `File.Replace()` 原子替换。
- 如果目标配置不存在，用 `File.Move()`。

配置损坏处理：

1. 捕获读取或反序列化异常。
2. 将原文件复制为 `config.corrupt.<timestamp>.json`。
3. 创建并保存默认配置。

## 17. 设置窗口

`SettingsForm` 是固定对话框，启用 `AutoScaleMode.Dpi`。

窗口属性：

- `StartPosition = CenterScreen`
- `FormBorderStyle = FixedDialog`
- 禁用最大化和最小化
- `ClientSize = 640 x 290`

可配置项：

- 空闲多久后切换到节能模式，单位秒
- 检测间隔，单位秒
- 活跃时电源计划
- 空闲时电源计划
- 当前用户开机自启

电源计划下拉框：

- 使用 `ComboBoxStyle.DropDownList`。
- 显示文本只显示计划名称，当前活跃计划后加 `*`。
- GUID 不在下拉框主文本中显示。
- tooltip 显示计划名称和 GUID。
- 下拉展开时根据最长计划名称动态调整 dropdown 宽度，最大 900 像素。

保存时：

1. 校验空闲阈值范围。
2. 校验检测间隔范围。
3. 校验活跃计划和空闲计划均已选择。
4. 从原配置 clone，写入新值。
5. 设置 `PowerPlansConfiguredByUser=true`。
6. Normalize。
7. `ApplySettings()` 同步 HKCU Run 自启状态。
8. 如果活跃计划是平衡计划，尝试自动修正为高性能计划。
9. 保存配置。
10. 如果未暂停，排队执行一次即时检测。

审查注意：

- 窗口固定大小虽启用了 DPI scaling，但极端长文本或特殊字体仍需人工 UI 复测。
- 下拉框只隐藏 GUID，不删除 GUID；实际保存仍使用 GUID。

## 18. 开机自启

开机自启通过当前用户注册表实现：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
Value name: AutoPowerMode
Value data: "<当前 exe 路径>"
```

行为：

- 默认 `AutoStart=false`。
- 启动时 `SyncStartupRegistration()` 会按配置同步注册表。
- `SetEnabled(true)` 写入当前进程路径，并加双引号。
- `SetEnabled(false)` 删除 Run 值。
- `IsEnabled()` 不只检查值是否存在，还会解析注册表值并确认它指向当前 exe。

`StartupRegistrationValue` 支持：

- 带双引号路径。
- 不带双引号但包含 `.exe` 的值。
- 大小写不敏感路径比较。
- `/` 和 `\` 归一化后比较。

审查注意：

- 当前实现不支持带命令行参数的开机启动策略，只提取 exe 路径。
- 若未来需要启动参数，需要扩展解析和写入策略。

## 19. 单实例机制

单实例由 `SingleInstanceService` 实现。

互斥锁名称：

```text
Local\AutoPowerMode.SingleInstance.<user-scope-suffix>
```

命名管道名称：

```text
AutoPowerMode.SingleInstance.<user-scope-suffix>
```

`user-scope-suffix` 优先使用当前用户 SID；如果读取 SID 失败，退回 `Environment.UserName`。字符串会过滤，只保留字母、数字、`.`、`-`、`_`。

流程：

1. 程序启动时创建命名互斥锁。
2. 如果是第一个实例，启动命名管道服务。
3. 如果不是第一个实例，连接本机命名管道，发送 `OpenSettings`。
4. 已运行实例收到 `OpenSettings` 后打开设置窗口。
5. 如果连接失败，新实例提示用户从托盘打开设置。

审查注意：

- 命名管道没有显式设置 ACL，依赖 .NET/Windows 默认安全描述符。
- 管道只接受一个命令：`OpenSettings`；不会执行任意命令。
- 同一用户或能访问该管道的本机进程可能触发设置窗口弹出。

## 20. 日志

日志写入两个位置，双日志策略仍保留：

```text
%AppData%\AutoPowerMode\logs\app.log
<程序所在目录>\logs\app.log
```

日志格式：

```text
[yyyy-MM-dd HH:mm:ss] [LEVEL] message
```

轮转策略：

- 单个 `app.log` 最大 `1MB`。
- 写入前如果当前大小加本次日志超过 1MB，先轮转。
- `app.log` 轮转为 `app.1.log`。
- `app.1.log` 轮转为 `app.2.log`。
- `app.2.log` 轮转为 `app.3.log`。
- 旧 `app.3.log` 会被删除。
- 最多保留 `app.log`、`app.1.log`、`app.2.log`、`app.3.log`。

路径脱敏：

- 当前 `%AppData%\AutoPowerMode` 路径尽量替换为 `%AppData%\AutoPowerMode`。
- Windows 用户 AppData 形式路径，例如 `C:\Users\<用户名>\AppData\Roaming\AutoPowerMode`，替换为 `%AppData%\AutoPowerMode`。
- 当前程序目录替换为 `<AppDirectory>`。
- `GetLogLocationMessage()` 返回的路径也会脱敏。

异常处理：

- 日志目录创建、轮转、写入失败都会被吞掉。
- 日志系统不能导致托盘程序崩溃。

审查注意：

- 异常堆栈仍可能包含未覆盖的路径格式。
- 脱敏是基础替换，不是完整 DLP。
- `powercfg` 输出中的电源计划名称和 GUID 会进入日志。

## 21. 本地文件和外部副作用

程序可能修改或创建：

| 位置 | 目的 |
| --- | --- |
| `%AppData%\AutoPowerMode\config.json` | 保存用户配置 |
| `%AppData%\AutoPowerMode\config.corrupt.<timestamp>.json` | 备份损坏配置 |
| `%AppData%\AutoPowerMode\logs\app.log` | 当前用户 AppData 日志 |
| `%AppData%\AutoPowerMode\logs\app.1.log` 到 `app.3.log` | AppData 历史日志 |
| `<程序目录>\logs\app.log` | 便携目录日志 |
| `<程序目录>\logs\app.1.log` 到 `app.3.log` | 便携目录历史日志 |
| `HKCU\...\Run\AutoPowerMode` | 当前用户开机自启 |
| Windows 当前电源计划 | 通过 `powercfg /setactive` 切换 |
| 本机互斥锁和命名管道 | 单实例和二次启动激活 |

程序不会主动：

- 发起 HTTP/HTTPS 请求。
- 上传日志或配置。
- 读取浏览器、cookie、SSH key、系统凭据。
- 修改机器级注册表 HKLM。
- 创建 Windows Service。
- 请求管理员权限。
- 安装驱动或计划任务。

## 22. 异常处理

入口层异常：

- 非 Windows 系统：弹窗提示后退出。
- WinForms UI 线程异常：写日志并弹窗提示日志位置。
- AppDomain 未处理异常：写日志。
- 启动失败：写日志并弹窗提示日志位置。

后台循环异常：

- 写日志。
- 等待 1 分钟后继续循环。

电源计划切换异常：

- 写日志。
- 不向用户弹窗，避免后台频繁打扰。

配置保存失败：

- 部分场景显示 warning 弹窗。
- `_configSaveFailureShown` 用于避免重复弹窗。

日志异常：

- 全部吞掉，不影响程序运行。

## 23. 测试覆盖

测试项目位于 `AutoPowerMode.Tests`，是 `net8.0` 控制台程序，不依赖 xUnit/NuGet。它通过链接源码文件测试纯逻辑，避免在 macOS 或非 Windows 环境运行 WindowsDesktop runtime。

当前测试数量：18。

覆盖项：

- 配置迁移：旧分钟字段按 `minutes * 60` 迁移。
- 配置迁移：秒字段优先于旧分钟字段。
- 默认 `AutoStart=false`。
- `powercfg` 英文输出解析。
- `powercfg` 简体中文输出解析。
- `powercfg` 繁体中文输出解析。
- OEM 和带括号计划名解析。
- 缺失标准计划时不凭空配置。
- GUID 大小写不敏感匹配。
- 状态机要求连续空闲确认。
- 状态机立即恢复 Active。
- 日志 1MB 轮转。
- 日志最多保留 3 个历史文件。
- 日志路径脱敏。
- 日志写入失败不抛异常。
- 自启注册表路径指向当前 exe 才视为启用。
- 自启路径加引号。
- 外部手动切换到自定义计划时自动切换跳过。

运行命令：

```bash
dotnet run --project AutoPowerMode.Tests
```

## 24. 发布方式

主项目配置：

- `OutputType=WinExe`
- `TargetFramework=net8.0-windows`
- `UseWindowsForms=true`
- `EnableWindowsTargeting=true`
- `Version=1.1.2`
- `AssemblyVersion=1.1.2.0`
- `FileVersion=1.1.2.0`

Release 构建禁用调试符号：

- `DebugType=none`
- `DebugSymbols=false`

GitHub Actions：

- workflow：`Build Windows Release`
- 触发方式：
  - 手动 `workflow_dispatch`
  - push `v*` tag
- 构建环境：`windows-latest`
- .NET SDK：`8.0.x`
- 发布命令：

```powershell
dotnet publish AutoPowerMode.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=false `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o publish
```

资产名：

```text
AutoPowerMode-<tag>-win-x64-framework-dependent.zip
```

v1.1.2 线上 zip 内容：

```text
AutoPowerMode-v1.1.2-win-x64-framework-dependent/
  AutoPowerMode.deps.json
  AutoPowerMode.dll
  AutoPowerMode.exe
  AutoPowerMode.runtimeconfig.json
  README.md
```

v1.1.2 发布包已核验未包含：

- PDB
- `.DS_Store`
- `__MACOSX`
- `artifacts/`
- 本机路径字符串

## 25. 安全和隐私边界

当前代码中的隐私相关数据主要在本机：

- Windows 用户 AppData 路径，日志会做基础脱敏。
- 程序目录，日志会做基础脱敏。
- 电源计划名称和 GUID。
- 当前 exe 路径，主要用于自启注册表。
- 当前用户 SID 或用户名，只用于构造本机互斥锁/管道名。
- 异常堆栈。

这些数据不会被网络发送。

发布前已设置 Release 不生成 PDB，减少源路径进入发布包的风险。

审查者应重点确认：

- 日志脱敏覆盖是否足够，尤其是异常堆栈中的路径。
- 命名管道是否需要显式当前用户 ACL。
- 双日志策略是否仍值得保留。
- 便携目录不可写时是否需要用户可见提示，目前是静默失败。

## 26. 当前已知取舍

1. 默认不开机自启
   - v1.1.2 默认 `AutoStart=false`。
   - 用户需要手动开启。

2. 自动配置仍可能修正用户配置的平衡计划
   - 如果活跃计划被配置成平衡计划，程序会尝试修正成高性能。
   - `PowerPlansConfiguredByUser` 当前没有阻止这个修正。

3. 外部手动切换保护较保守
   - 用户切到非配置计划后，自动切换会跳过。
   - 当前没有明显 UI 提示，仅写日志。

4. 状态机阈值固定
   - Active -> Idle 连续确认次数写死为默认 2。
   - UI 没有暴露这个参数。

5. 设置窗口仍是固定尺寸
   - 已支持 DPI scaling 和长下拉框宽度。
   - 仍需 Windows 高 DPI、长 OEM 名称和多语言环境人工复测。

6. 测试是轻量自研测试入口
   - 优点是无 NuGet 依赖、可在本机跑纯逻辑。
   - 缺点是没有标准 test runner、测试报告和 IDE 集成。

7. 没有真实 Windows 集成测试
   - `powercfg`、WinForms UI、注册表和 `GetLastInputInfo` 的真实行为仍需要 Windows 手工或自动化验证。

## 27. 建议其他智能体重点审查的问题

1. `PowerPlansConfiguredByUser` 是否应该阻止 `TryAutoConfigure()` 自动覆盖用户配置？
2. 外部手动切换保护是否应该在托盘菜单显示“检测到外部计划，自动切换暂停/跳过”？
3. `activeResumeThreshold` 是否应该独立于 `idleThreshold`，例如固定小于阈值或可配置？
4. `Active -> Idle` 连续确认次数是否应该可配置，或根据检测间隔自动调整？
5. 命名管道是否需要显式 ACL 限制到当前用户 SID？
6. 日志脱敏是否应覆盖更多 Windows 路径模式，例如 `%LOCALAPPDATA%`、桌面、下载目录？
7. 双日志策略是否会在安装目录可写时留下不必要的便携日志？
8. 配置原子保存失败后，临时文件是否需要清理策略？
9. `powercfg` 输出解析是否需要更结构化的 fallback，例如按 GUID 先切分再解析名称？
10. 是否需要在 README/Release notes 中加入更明确的 .NET Desktop Runtime 下载链接？
11. 是否需要新增 Windows UI 自动化测试，验证高 DPI 和长名称不截断？
12. 是否应该将轻量自研测试迁移到 xUnit/NUnit/MSTest，便于 CI 测试报告？
13. 是否需要日志保留天数或压缩，而不是只按大小轮转？
14. 是否需要在托盘菜单中显示当前“外部手动覆盖”状态？
15. 是否需要对 `powercfg /setactive` 失败进行用户可见提示或设置页诊断入口？

## 28. 快速验证命令

运行纯逻辑测试：

```bash
dotnet run --project AutoPowerMode.Tests
```

本地构建主项目：

```bash
dotnet build AutoPowerMode.csproj -c Release
```

本地发布 framework-dependent 包：

```bash
dotnet publish AutoPowerMode.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -p:PublishSingleFile=false \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -o release/AutoPowerMode-v1.1.2-win-x64-framework-dependent
```

检查发布 zip 内容：

```bash
unzip -l release/AutoPowerMode-v1.1.2-win-x64-framework-dependent.zip
```

检查 zip 是否混入 macOS 或调试文件：

```bash
zipinfo -1 release/AutoPowerMode-v1.1.2-win-x64-framework-dependent.zip \
  | rg -i '(__MACOSX|\.DS_Store|\.pdb|artifacts/|release/)'
```

预期：无命中。

检查发布二进制是否含本机路径：

```bash
tmpdir=$(mktemp -d)
unzip -q release/AutoPowerMode-v1.1.2-win-x64-framework-dependent.zip -d "$tmpdir"
strings -a "$tmpdir"/AutoPowerMode-v1.1.2-win-x64-framework-dependent/AutoPowerMode.exe \
  "$tmpdir"/AutoPowerMode-v1.1.2-win-x64-framework-dependent/AutoPowerMode.dll \
  | rg -i '(/Users/|C:\\Users|AutoPowerMode\.pdb|SourceRoot)'
```

预期：无命中。

## 29. 一句话交接摘要

AutoPowerMode v1.1.2 是一个无联网、无遥测的 Windows 托盘程序，通过 `GetLastInputInfo` 判断用户空闲时间，通过 `powercfg` 切换当前系统电源计划，通过 HKCU Run 实现当前用户开机自启；v1.1.2 重点增强了状态边界、配置迁移、`powercfg` 解析测试、默认关闭自启、日志轮转和路径脱敏，并已发布为 Windows x64 framework-dependent zip。
