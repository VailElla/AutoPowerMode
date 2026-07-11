# AutoPowerMode v1.1.1 功能审查说明

本文档用于交给其他智能体或审查者，对 AutoPowerMode v1.1.1 做功能、代码安全、隐私、可靠性和后续优化审查。内容基于当前仓库源码重新核验，不依赖聊天摘要。

## 1. 审查对象

- 项目名：AutoPowerMode
- 当前应用版本：`v1.1.1`
- 应用类型：Windows-only WinForms 桌面托盘程序
- 目标框架：`.NET 8`，`net8.0-windows`
- 发布形态：Windows x64 framework-dependent，不内置 .NET 运行时
- 运行前置条件：目标机器需要安装 `.NET 8 Desktop Runtime`
- 当前仓库远端：`https://github.com/VailElla/AutoPowerMode.git`
- `v1.1.1` tag 对应提交：`296d3d9`，当前 `main` 还包含 release notes 修复提交 `4d12308`

## 2. 产品目标

AutoPowerMode 的目标是根据当前用户是否正在使用电脑，自动切换 Windows 电源计划：

- 用户活跃时切换到高性能计划。
- 用户空闲达到阈值后切换到节能计划。
- 程序常驻系统托盘，不显示主窗口。
- 用户可通过托盘菜单暂停、恢复、手动切换计划、打开设置、设置开机自启或退出。

该程序没有远程账号、云同步、网络更新、遥测、崩溃上报或后台服务。

## 3. 仓库结构和模块职责

```text
AutoPowerMode/
  AppInfo.cs                         版本和显示名
  AppConfig.cs                       配置模型、默认值和范围约束
  ConfigService.cs                   配置读写、旧配置迁移、损坏配置备份
  IdleDetector.cs                    Win32 GetLastInputInfo 空闲时间检测
  Logger.cs                          本地日志写入
  PowerPlan.cs                       电源计划数据模型
  PowerPlanManager.cs                powercfg 调用、计划解析、自动匹配和切换
  Program.cs                         程序入口、Windows 检查、异常处理、单实例启动
  SettingsForm.cs                    WinForms 设置窗口
  SingleInstanceService.cs           单实例互斥锁和本机命名管道激活
  StartupService.cs                  当前用户级别开机自启注册表
  TrayAppContext.cs                  托盘 UI、后台检测循环、状态机和核心调度
  AutoPowerMode.csproj               .NET 项目配置和 Release 调试符号设置
  .github/workflows/release.yml      GitHub Actions 发布流程
  README.md                          用户说明
```

## 4. 功能总览

### 4.1 系统托盘

程序启动后创建 `NotifyIcon`，默认图标为 `SystemIcons.Application`，不打开主窗口。

托盘菜单包含：

- 版本：显示 `AppInfo.Version`
- 当前状态：`Active`、`Idle`、`Paused` 或 `NotConfigured`
- 当前电源计划名称
- 空闲阈值，单位秒
- 检测间隔，单位秒
- 暂停/恢复自动切换
- 立即切换到高性能计划
- 立即切换到节能计划
- 设置
- 开机自启状态切换
- 退出

托盘图标双击会打开设置窗口。再次运行 exe 时，已有实例会收到本机命名管道消息并打开设置窗口。

### 4.2 自动切换电源计划

核心循环在 `TrayAppContext.MonitorLoopAsync` 中运行：

1. 读取当前配置快照。
2. 如果 `IsPaused=true`，状态设为 `Paused`，不切换。
3. 如果未配置有效的活跃计划和空闲计划，状态设为 `NotConfigured`，不切换。
4. 调用 `IdleDetector.GetIdleTime()` 获取当前用户距离最后一次输入的空闲时间。
5. 如果空闲时间大于等于 `IdleThresholdSeconds`，切换到空闲计划。
6. 否则切换到活跃计划。
7. 等待 `CheckIntervalSeconds` 后重复。

默认值：

- 空闲阈值：`1200` 秒
- 检测间隔：`10` 秒
- 自动启动：默认 `true`
- 暂停：默认 `false`

数值约束：

- 空闲阈值最小 `10` 秒，最大 `14400` 秒
- 检测间隔最小 `5` 秒，最大 `3600` 秒

### 4.3 手动切换

托盘菜单可以立即切换到高性能计划或节能计划。

手动切换调用同一套 `SwitchToConfiguredPlanAsync` 和 `SwitchToPlanAsync` 逻辑，但 `manual=true`。这意味着手动切换不会受 10 秒冷却时间限制。

### 4.4 自动切换冷却

自动切换有 10 秒冷却：

```text
SwitchCooldown = 10 seconds
```

如果不是手动切换，并且距离上次成功切换不足 10 秒，程序会跳过本次切换。这个设计用于避免状态边界附近频繁调用 `powercfg /setactive`。

### 4.5 暂停和恢复

托盘菜单的暂停/恢复会修改配置中的 `IsPaused`：

- 暂停时立即保存配置，状态变为 `Paused`
- 恢复时立即保存配置，并排队执行一次即时检测

暂停状态会持久化到配置文件，下次启动仍会保留。

### 4.6 设置窗口

设置窗口由 `SettingsForm` 实现，是固定大小 WinForms 对话框。

可配置项：

- 空闲多久后切换到节能模式，单位秒
- 检测间隔，单位秒
- 活跃时电源计划
- 空闲时电源计划
- 当前用户开机自启

设置窗口使用下拉框展示系统电源计划。显示格式：

```text
<计划名称> (<GUID>)*
```

当前活跃计划会附加 `*`。

保存时：

1. 校验数值范围。
2. 校验活跃计划和空闲计划均已选择。
3. 将修改写回 `AppConfig`。
4. 标记 `PowerPlansConfiguredByUser=true`。
5. 同步开机自启注册表。
6. 保存配置。
7. 如果未暂停，立即排队执行一次检测。

取消时不保存。

## 5. 状态模型

`PowerModeState` 有四个状态：

| 状态 | 含义 |
| --- | --- |
| `Active` | 当前逻辑状态为活跃，目标应为活跃电源计划 |
| `Idle` | 当前逻辑状态为空闲，目标应为空闲电源计划 |
| `Paused` | 用户暂停自动切换 |
| `NotConfigured` | 缺少有效电源计划配置，无法自动切换 |

状态更新逻辑：

- `IsPaused=true` 时显示 `Paused`
- 配置无效时显示 `NotConfigured`
- 配置有效时显示内部 `_currentState`
- `_currentState` 会在成功切换或确认当前已是目标计划后更新

审查注意：启动后如果配置有效，但第一次检测前 `_currentState` 初始值为 `NotConfigured`，第一次检测会尝试把当前系统计划调整到目标状态。

## 6. 配置文件

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
  "autoStart": true,
  "isPaused": false,
  "powerPlansConfiguredByUser": false
}
```

读配置时支持：

- JSON 注释
- 尾随逗号
- 旧字段 `idleThresholdMinutes`
- 旧字段 `checkIntervalMinutes`

旧分钟字段迁移策略：

- 如果发现 `idleThresholdMinutes` 但没有 `idleThresholdSeconds`，不会按分钟值换算，而是重置为默认 `1200` 秒。
- 如果发现 `checkIntervalMinutes` 但没有 `checkIntervalSeconds`，不会按分钟值换算，而是重置为默认 `10` 秒。

配置损坏处理：

1. 捕获读取或反序列化异常。
2. 将原文件复制为 `config.corrupt.<timestamp>.json`。
3. 创建并保存默认配置。

写配置策略：

- 先写入临时文件 `config.<processId>.<guid>.tmp`
- 如果目标配置存在，用 `File.Replace` 原子替换
- 如果目标配置不存在，用 `File.Move`

## 7. 日志行为

日志写入两个位置：

```text
%AppData%\AutoPowerMode\logs\app.log
<程序所在目录>\logs\app.log
```

日志格式：

```text
[yyyy-MM-dd HH:mm:ss] [LEVEL] message
```

日志写入失败会被吞掉，不能让日志故障导致托盘程序崩溃。

当前没有日志轮转、大小限制、保留天数或隐私脱敏层。

日志内容可能包含：

- 配置路径
- 便携日志路径
- 电源计划名称和 GUID
- `powercfg` 的 stdout/stderr
- 异常堆栈
- 当前用户 AppData 路径

审查注意：日志是本地文件，不会外传；但如果用户手动上传日志，可能包含 Windows 用户名路径和电源计划信息。

## 8. 电源计划处理

`PowerPlanManager` 通过 Windows 内置 `powercfg` 完成所有电源计划操作。

调用命令：

```text
powercfg /list
powercfg /getactivescheme
powercfg /setactive <guid>
```

进程启动设置：

- `UseShellExecute=false`
- `CreateNoWindow=true`
- 重定向 stdout/stderr
- 超时 10 秒
- 超时后尝试终止整个进程树

计划解析：

- 使用正则从 `powercfg` 输出中提取 GUID、名称和是否活跃
- 当前正则匹配 GUID + 括号中的计划名 + 可选 `*`

自动匹配规则：

空闲计划候选：

- 名称精确或包含：`Power Saver`、`节能`、`節能`、`省电`、`省電`、`节电`、`節電`
- 或 GUID 为 Windows 标准 Power Saver GUID：`a1841308-3541-4fab-bc81-f71556f20b4a`

活跃计划候选：

- 名称精确或包含：`High Performance`、`高性能`、`高效能`
- 或 GUID 为 Windows 标准 High Performance GUID：`8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c`

平衡计划识别：

- GUID 为 `381b4222-f694-41f0-9685-ff5bb260df2e`
- 或名称为 `Balanced`、`平衡`、`平衡模式`

如果活跃计划被配置成平衡计划，程序会尝试自动修正为高性能计划。

审查注意：

- `PowerPlansConfiguredByUser` 会被保存，但当前 `TryAutoConfigure` 不使用它来阻止自动修正。
- 如果系统不存在高性能或节能计划，程序会进入 `NotConfigured` 或需要用户手动选择。
- `powercfg /setactive` 通常不需要管理员权限，但系统策略、设备类型或 OEM 定制可能影响结果。

## 9. 空闲检测

`IdleDetector` 使用 Win32 API：

```text
user32.dll!GetLastInputInfo
```

逻辑：

1. 创建 `LASTINPUTINFO` 结构。
2. 设置结构大小。
3. 调用 `GetLastInputInfo` 获取最后一次输入 tick。
4. 使用 `Environment.TickCount` 和最后输入 tick 计算空闲毫秒数。

计算使用 `unchecked((uint)Environment.TickCount)`，能处理 32 位 tick 溢出场景。

检测范围是当前 Windows 会话的鼠标和键盘输入，不是应用内部输入事件。

## 10. 开机自启

开机自启通过当前用户注册表实现：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
Value name: AutoPowerMode
Value data: "<当前 exe 路径>"
```

行为：

- `IsEnabled()` 只检查 Run 项中是否存在非空字符串，不校验路径是否仍然指向当前 exe。
- `SetEnabled(true)` 写入当前进程路径，并加双引号。
- `SetEnabled(false)` 删除值。
- 应用启动时会调用 `SyncStartupRegistration()`，按配置中的 `AutoStart` 同步注册表。

审查注意：

- 默认配置 `AutoStart=true`，首次启动时会自动写入当前用户 Run 项。
- 如果 exe 移动位置，下一次启动且 `AutoStart=true` 时会更新 Run 项路径。

## 11. 单实例机制

单实例由 `SingleInstanceService` 实现。

互斥锁名称：

```text
Local\AutoPowerMode.SingleInstance.<user-scope-suffix>
```

命名管道名称：

```text
AutoPowerMode.SingleInstance.<user-scope-suffix>
```

`user-scope-suffix` 优先使用当前用户 SID；如果获取 SID 失败，退回 `Environment.UserName`。字符串会过滤，只保留字母、数字、`.`、`-`、`_`。

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

## 12. 异常处理

入口层异常处理：

- 非 Windows 系统：弹窗提示后退出。
- WinForms UI 线程异常：写日志并弹窗提示日志位置。
- AppDomain 未处理异常：写日志。
- 启动失败：写日志并弹窗提示日志位置。

后台检测循环异常：

- 写日志。
- 等待 1 分钟后继续循环。

切换计划异常：

- 写日志。
- 不向用户弹窗，避免后台频繁打扰。

配置保存失败：

- 某些场景显示 warning 弹窗。
- `_configSaveFailureShown` 用于避免重复弹窗。

## 13. 发布方式

项目文件：

- `OutputType=WinExe`
- `TargetFramework=net8.0-windows`
- `UseWindowsForms=true`
- `EnableWindowsTargeting=true`
- Release 构建禁用调试符号：
  - `DebugType=none`
  - `DebugSymbols=false`

GitHub Actions：

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

发布资产名称：

```text
AutoPowerMode-<tag>-win-x64-framework-dependent.zip
```

发布包内容：

```text
AutoPowerMode.exe
AutoPowerMode.dll
AutoPowerMode.deps.json
AutoPowerMode.runtimeconfig.json
README.md
```

发布包不内置 .NET Runtime，所以体积很小，但用户必须安装 .NET 8 Desktop Runtime。

## 14. 本地文件和 Git 忽略规则

以下本地输出不会被提交：

```text
bin/
obj/
artifacts/
release/
*.zip
.DS_Store
logs/
config.json
*.log
```

`release/` 是本地测试和手动上传前的可发布内容目录；GitHub 正式 Release 资产优先由 GitHub Actions 生成。

## 15. 外部副作用清单

程序可能修改或创建：

| 位置 | 目的 |
| --- | --- |
| `%AppData%\AutoPowerMode\config.json` | 保存用户配置 |
| `%AppData%\AutoPowerMode\config.corrupt.<timestamp>.json` | 备份损坏配置 |
| `%AppData%\AutoPowerMode\logs\app.log` | 本地日志 |
| `<程序目录>\logs\app.log` | 便携日志 |
| `HKCU\...\Run\AutoPowerMode` | 当前用户开机自启 |
| Windows 当前电源计划 | 通过 `powercfg /setactive` 切换 |
| 本机互斥锁和命名管道 | 单实例和二次启动激活 |

程序不会主动：

- 发起 HTTP/HTTPS 请求
- 上传日志或配置
- 读取浏览器、cookie、SSH key、系统凭据
- 修改机器级注册表 HKLM
- 创建 Windows Service
- 请求管理员权限
- 安装驱动或计划任务

## 16. 隐私和安全边界

当前代码中的隐私相关数据主要在本机：

- Windows 用户 AppData 路径
- 电源计划名称和 GUID
- 当前 exe 路径
- 当前用户 SID 或用户名只用于构造本机互斥锁/管道名
- 异常堆栈

这些数据不会被网络发送。

发布前已设置 Release 不生成 PDB，减少源路径进入发布包的风险。

审查者应重点确认：

- 发布 zip 中没有 PDB、`.DS_Store`、`__MACOSX`、`artifacts/` 或本机路径字符串。
- Git 历史和当前源码没有明文 token、cookie、私钥或本机绝对路径。
- 日志内容是否需要降噪或脱敏，尤其是异常堆栈和配置路径。

## 17. 可靠性和优化审查重点

以下不是必须修改项，而是建议其他智能体重点审查的地方：

1. 默认开机自启为 `true`
   - 首次启动即写入 HKCU Run。
   - 需要判断这是否符合用户期望，或改成首次打开设置时由用户明确选择。

2. `PowerPlansConfiguredByUser` 当前没有阻止自动修正
   - 用户手动选择后，后续 `TryAutoConfigure` 仍可能在某些情况下覆盖配置。
   - 可审查是否应尊重该字段，减少意外更改。

3. 命名管道缺少显式 ACL
   - 当前风险较低，因为只支持 `OpenSettings`。
   - 仍可考虑限制为当前用户 SID。

4. 日志没有轮转
   - 长时间运行后 `app.log` 可能持续增长。
   - 可增加大小上限、按日期切分或保留天数。

5. 便携日志写入程序目录
   - 如果程序目录不可写，当前会静默失败。
   - 如果程序目录可写，会在软件目录产生 `logs/`。
   - 可评估是否需要保留双日志策略。

6. `powercfg` 输出解析依赖正则
   - 当前针对常见 Windows 输出格式。
   - 不同系统语言或格式变化可能导致解析失败。
   - 可增加样本测试。

7. 无自动测试
   - 当前项目没有单元测试或集成测试。
   - 可对配置迁移、计划解析、GUID 匹配、状态机逻辑做测试。

8. 设置窗口固定尺寸
   - 中文环境可读。
   - 高 DPI、英文系统、长电源计划名称可能需要审查。

9. Release notes 和 README 需要保持一致
   - v1.1.1 是 framework-dependent。
   - 不要再次混用 self-contained 文案。

10. 用户机器缺少 .NET Desktop Runtime 时的体验
    - framework-dependent 包体积小，但依赖用户安装运行时。
    - 可考虑在 README 或 Release notes 提供官方 runtime 下载入口。

## 18. 建议审查问题清单

交给其他智能体时，可以直接要求它回答：

1. 当前默认 `AutoStart=true` 是否应该改成默认关闭？
2. `PowerPlansConfiguredByUser` 是否应该参与自动配置逻辑？
3. 命名管道是否需要显式当前用户 ACL？
4. 日志是否可能记录过多本机信息？需要哪些脱敏？
5. `powercfg` 解析是否覆盖英文、简体中文、繁体中文系统？
6. 设置窗口是否适配高 DPI 和长名称？
7. 是否需要为配置迁移和 `ParsePlans` 增加测试？
8. v1.1.1 发布包是否应补充 runtime 下载说明？
9. 是否需要保留程序目录便携日志？
10. 是否需要更明确地区分手动切换状态和自动检测状态？

## 19. 快速验证命令

本地构建 framework-dependent 包：

```bash
DOTNET_CLI_HOME=../.dotnet-home DOTNET_ROOT=../.dotnet ../.dotnet/dotnet publish AutoPowerMode.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -p:PublishSingleFile=false \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -o release/AutoPowerMode-v1.1.1-win-x64-framework-dependent
```

检查 Git 未跟踪发布产物不会被提交：

```bash
git status --ignored --short
git ls-files --others --exclude-standard
```

检查 zip 内容：

```bash
unzip -l release/AutoPowerMode-v1.1.1-win-x64-framework-dependent.zip
```

检查发布二进制是否含本机路径：

```bash
strings -a release/AutoPowerMode-v1.1.1-win-x64-framework-dependent/AutoPowerMode.exe \
  release/AutoPowerMode-v1.1.1-win-x64-framework-dependent/AutoPowerMode.dll \
  | rg -i '(/Users/|C:\\Users|AutoPowerMode\.pdb|SourceRoot)'
```

预期：无命中。

## 20. 一句话交接摘要

AutoPowerMode v1.1.1 是一个无联网、无遥测的 Windows 托盘程序，通过 `GetLastInputInfo` 判断用户空闲时间，通过 `powercfg` 切换当前用户机器的电源计划，通过 HKCU Run 实现当前用户开机自启；默认发布包为小体积 framework-dependent zip，需要用户安装 .NET 8 Desktop Runtime，审查重点应放在默认自启、配置自动修正、命名管道 ACL、日志隐私和 `powercfg` 解析可靠性。
