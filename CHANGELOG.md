# AutoPowerMode Changelog

## [v1.3.0] - 2026-07-11

- 新增两个默认关闭、可独立勾选的空闲误触保护：识别其他程序声明的 `ES_SYSTEM_REQUIRED`、`ES_DISPLAY_REQUIRED`、`ES_AWAYMODE_REQUIRED`，以及当前前台窗口是否全屏；保护条件成立时不应用空闲规则。
- 检测改为双频固定策略：活跃或未知状态每 30 秒检测，进入空闲计划后每 1 秒检测，以便用户回来时快速恢复。
- 设置、托盘状态、诊断信息、配置持久化和中英文文档已同步更新；旧版单一检测间隔字段在下次保存时移除。
- 补充执行状态位、全屏边界、保护取消连续空闲确认、双频检测和配置持久化测试。

- Added two independent, opt-in idle protections, disabled by default: Windows execution-state requests (`ES_SYSTEM_REQUIRED`, `ES_DISPLAY_REQUIRED`, and `ES_AWAYMODE_REQUIRED`) and fullscreen foreground windows.
- Replaced the configurable single interval with fixed dual-rate monitoring: every 30 seconds while active or unknown, and every second after entering the idle plan.
- Updated settings, tray status, diagnostics, configuration persistence, and bilingual documentation. Obsolete single-interval fields are removed the next time configuration is saved.
- Added tests for execution-state flags, fullscreen bounds, protected idle confirmations, dual-rate monitoring, and configuration persistence.

## [v1.2.0] - 2026-07-11

- 新增英文与简体中文界面；中文 Windows 系统文化默认使用简体中文，其他系统文化默认使用英文。
- 设置页新增语言选择，可在跟随系统、English 和简体中文之间主动切换，选择结果仅保存在本地配置中。
- 托盘菜单、设置、通知、诊断和错误提示全部接入同一套本地化文本。
- GitHub 首页新增文档顶部语言入口：`English | 简体中文`，并提供完整双语 README。
- 新增隐私说明和发布安全检查：无遥测、分析、后台上传或远程 API；GitHub 链接只在用户主动点击时由默认浏览器打开。
- 增加系统语言识别、手动语言覆盖和配置持久化测试。

- Added English and Simplified Chinese UI. Chinese Windows cultures default to Simplified Chinese; every other culture defaults to English.
- Added an in-app language selector with system, English, and Simplified Chinese choices stored only in the local configuration.
- Localized the tray menu, settings, notifications, diagnostics, and user-facing errors.
- Added `English | 简体中文` navigation at the top of both GitHub README files.
- Documented and verified the local-only privacy model: no telemetry, analytics, background uploads, or remote API client.

## [v1.1.6] - 2026-07-11

- 设置窗口改为 Per-Monitor V2 DPI 模式，在窗口移动到不同缩放比例的显示器时动态重排。
- 移除 780 x 430 的较大固定最小尺寸，初始逻辑尺寸调整为约 460 x 270，最小逻辑尺寸约 400 x 240。
- 标签列和单位列改为内容自动宽度，设置行改为内容自动高度，减少空白并支持更紧凑窗口。
- 窗口尺寸会按当前 DPI 计算并受显示器工作区限制，内容超出时保留滚动兜底。
- 增加 100%、125%、150%、175%、200%、225% 和 250% 的 DPI 尺寸策略测试。

## [v1.1.5] - 2026-07-11

- 修复 Windows 150% 缩放下设置页左侧标签和通知选项被截断的问题，扩大最小尺寸并允许用户调整窗口。
- 缩短启动、切换成功、切换失败和外部改动通知，移除通知中的技术性确认描述。
- 检测间隔可在 1-60 秒之间自由设置，移除“当前设置正常”和建议区间文案。
- 设置页新增 `GitHub 项目主页` 按钮，使用系统默认浏览器打开项目链接。
- 仓库整理为 `src/`、`tests/`、`docs/` 和 `archive/`；历史构建保留在 Git 忽略的归档目录，清理可再生成缓存和 macOS 元数据文件。

## [v1.1.4] - 2026-07-11

- 新增电源计划通知，区分程序启动同步、实际切换成功、切换失败以及外部改动，不再把“当前已是目标计划”误报为成功切换。
- 每次执行 `powercfg /setactive` 后重新读取当前电源计划，只有实际 GUID 与目标 GUID 一致时才记为切换成功。
- 切换失败时会显示故障通知并继续自动重试；同一目标的连续故障只通知一次，恢复后才重置。
- 设置窗口新增通知开关，默认开启；旧版配置缺少该字段时会自动使用默认值。
- 增加通知配置持久化和通知语义分类测试。

## [v1.1.3] - 2026-07-05

- 新增外部手动切换保护：如果用户在 Windows 里手动切到非 AutoPowerMode 配置的电源计划，程序会进入外部覆盖状态，避免自动切换马上覆盖用户选择。
- 托盘菜单新增恢复 AutoPowerMode 自动控制入口，用户可以从外部覆盖状态一键回到自动切换。
- 新增只读诊断窗口，支持复制诊断信息、打开日志目录、重新检测电源计划，便于排查配置和电源计划识别问题。
- 设置窗口显示当前自动切换运行状态，并在检测间隔较长时提示切换延迟。
- 增强配置读取和电源计划解析的防护，补充日志轮转、诊断快照和状态切换相关测试。

## [v1.1.2] - 2026-07-05

- 活跃切换到空闲前增加连续 2 次检测确认，减少刚到阈值附近的误判。
- 空闲切回活跃不受冷却限制，检测到鼠标或键盘输入后尽快恢复活跃计划。
- 自动切到空闲计划保留 10 秒内部冷却，用于限制重复调用 `powercfg /setactive`。
- 开机自启默认关闭，并改为用户手动开启，降低首次运行对系统设置的影响。
- 增加外部手动电源计划覆盖保护策略，避免程序覆盖用户临时选择的其他计划。
- 本地日志增加 1MB 轮转、最多保留 3 个历史文件，并对 `%AppData%\AutoPowerMode` 和程序目录做基础脱敏。
- 新增纯逻辑测试项目，覆盖配置迁移、`powercfg` 输出解析、标准 GUID 匹配、状态边界、自启路径判断和外部手动切换保护。
- Release 增加 `SHA256SUMS` 校验文件，便于核对下载包完整性。

## [v1.1.1] - 2026-07-05

- 默认发布包改为 Windows x64 framework-dependent，不再内置 .NET 运行时，压缩包体积从自包含版本的大包降到约 90KB。
- Release 构建禁用调试符号，减少发布包里出现本机源码路径或调试信息的风险。
- GitHub Actions 负责构建和上传整个发布目录，避免手动上传旧本地构建包。
- README 精简为面向使用者的说明，突出运行前需要安装 .NET 8 Desktop Runtime、未签名程序提示和核心功能列表。

## [v1.0.8] - 2026-07-05

- 提供 Windows x64 可下载发布包，用户解压后可直接运行 `AutoPowerMode.exe`。
- 程序以系统托盘方式运行，根据鼠标和键盘空闲时间自动切换活跃/空闲电源计划。
- 支持暂停和恢复自动切换、托盘菜单手动切换电源计划、设置空闲阈值和检测间隔。
- 支持当前用户开机自启、单实例运行、AppData 配置持久化、损坏配置备份恢复和本地日志。
