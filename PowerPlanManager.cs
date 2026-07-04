using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoPowerMode;

public sealed class PowerPlanManager
{
    private const string PowerSaverGuid = "a1841308-3541-4fab-bc81-f71556f20b4a";
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private static readonly Regex PlanRegex = new(
        @"(?i)([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\s+\(([^)]*)\)\s*(\*)?",
        RegexOptions.Compiled);

    public List<PowerPlan> GetPowerPlans()
    {
        var result = RunPowerCfg("/list");
        if (!result.Success)
        {
            Logger.Error($"powercfg /list 执行失败。ExitCode={result.ExitCode}; Error={result.Error}");
            return [];
        }

        var plans = ParsePlans(result.Output);
        if (plans.Count == 0)
        {
            Logger.Error($"powercfg /list 解析失败或没有找到电源计划。Output={result.Output}");
        }
        else
        {
            Logger.Info("检测到的电源计划列表：" + string.Join("; ", plans.Select(p => $"{p.Name} ({p.Guid}) Active={p.IsActive}")));
        }

        return plans;
    }

    public PowerPlan? GetActivePowerPlan()
    {
        var result = RunPowerCfg("/getactivescheme");
        if (!result.Success)
        {
            Logger.Error($"powercfg /getactivescheme 执行失败。ExitCode={result.ExitCode}; Error={result.Error}");
            return null;
        }

        var plan = ParsePlans(result.Output).FirstOrDefault();
        if (plan is null)
        {
            Logger.Error($"当前活跃电源计划解析失败。Output={result.Output}");
            return null;
        }

        plan.IsActive = true;
        Logger.Info($"当前活跃电源计划：{plan.Name} ({plan.Guid})");
        return plan;
    }

    public bool SetActivePlan(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            Logger.Error("目标电源计划 GUID 为空，无法切换。");
            return false;
        }

        var activePlan = GetActivePowerPlan();
        if (GuidEquals(activePlan?.Guid, guid))
        {
            Logger.Info($"当前已是目标电源计划，跳过切换：{guid}");
            return true;
        }

        var result = RunPowerCfg($"/setactive {guid}");
        if (!result.Success)
        {
            Logger.Error($"powercfg /setactive 执行失败。Guid={guid}; ExitCode={result.ExitCode}; Error={result.Error}; Output={result.Output}");
            return false;
        }

        Logger.Info($"电源计划已切换：{guid}");
        return true;
    }

    public bool TryAutoConfigure(AppConfig config, IReadOnlyList<PowerPlan> plans)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(config.IdlePowerPlanGuid) || !ContainsGuid(plans, config.IdlePowerPlanGuid))
        {
            var idlePlan = FindIdlePlan(plans);
            if (idlePlan is not null)
            {
                config.IdlePowerPlanGuid = idlePlan.Guid;
                changed = true;
                Logger.Info($"自动匹配到节能计划：{idlePlan.Name} ({idlePlan.Guid})");
            }
            else
            {
                Logger.Error("自动匹配节能计划失败。");
            }
        }

        if (string.IsNullOrWhiteSpace(config.ActivePowerPlanGuid) || !ContainsGuid(plans, config.ActivePowerPlanGuid))
        {
            var activePlan = FindActivePlan(plans);
            if (activePlan is not null)
            {
                config.ActivePowerPlanGuid = activePlan.Guid;
                changed = true;
                Logger.Info($"自动匹配到高性能计划：{activePlan.Name} ({activePlan.Guid})");
            }
            else
            {
                Logger.Error("自动匹配高性能计划失败。");
            }
        }
        else if (IsBalancedPlan(FindByGuid(plans, config.ActivePowerPlanGuid)))
        {
            var activePlan = FindActivePlan(plans);
            if (activePlan is not null)
            {
                Logger.Info($"检测到平衡计划被配置为活跃计划，已自动修正为高性能计划：{activePlan.Name} ({activePlan.Guid})");
                config.ActivePowerPlanGuid = activePlan.Guid;
                changed = true;
            }
        }

        return changed;
    }

    public PowerPlan? FindByGuid(IReadOnlyList<PowerPlan> plans, string guid)
    {
        return plans.FirstOrDefault(plan => GuidEquals(plan.Guid, guid));
    }

    public bool IsBalancedPlanGuid(IReadOnlyList<PowerPlan> plans, string guid)
    {
        return IsBalancedPlan(FindByGuid(plans, guid));
    }

    private static bool IsBalancedPlan(PowerPlan? plan)
    {
        if (plan is null)
        {
            return false;
        }

        return GuidEquals(plan.Guid, BalancedGuid)
               || string.Equals(plan.Name, "Balanced", StringComparison.CurrentCultureIgnoreCase)
               || string.Equals(plan.Name, "平衡", StringComparison.CurrentCultureIgnoreCase)
               || string.Equals(plan.Name, "平衡模式", StringComparison.CurrentCultureIgnoreCase);
    }

    private static PowerPlan? FindIdlePlan(IReadOnlyList<PowerPlan> plans)
    {
        return FindByName(plans, "Power Saver", "节能", "節能", "省电", "省電", "节电", "節電")
               ?? plans.FirstOrDefault(plan => GuidEquals(plan.Guid, PowerSaverGuid));
    }

    private static PowerPlan? FindActivePlan(IReadOnlyList<PowerPlan> plans)
    {
        return FindByName(plans, "High Performance", "高性能", "高效能")
               ?? plans.FirstOrDefault(plan => GuidEquals(plan.Guid, HighPerformanceGuid));
    }

    private static PowerPlan? FindByName(IReadOnlyList<PowerPlan> plans, params string[] names)
    {
        foreach (var name in names)
        {
            var exact = plans.FirstOrDefault(plan => string.Equals(plan.Name, name, StringComparison.CurrentCultureIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        foreach (var name in names)
        {
            var contains = plans.FirstOrDefault(plan => plan.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));
            if (contains is not null)
            {
                return contains;
            }
        }

        return null;
    }

    private static bool ContainsGuid(IReadOnlyList<PowerPlan> plans, string guid)
    {
        return plans.Any(plan => GuidEquals(plan.Guid, guid));
    }

    private static bool GuidEquals(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static List<PowerPlan> ParsePlans(string output)
    {
        var plans = new List<PowerPlan>();

        foreach (var line in output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = PlanRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            plans.Add(new PowerPlan
            {
                Guid = match.Groups[1].Value,
                Name = match.Groups[2].Value.Trim(),
                IsActive = match.Groups[3].Success
            });
        }

        return plans;
    }

    private static PowerCfgResult RunPowerCfg(string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!process.Start())
            {
                return new PowerCfgResult(false, -1, string.Empty, "powercfg 进程启动失败。");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(10_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception killException)
                {
                    Logger.Error("powercfg 超时后终止进程失败。", killException);
                }

                return new PowerCfgResult(false, -1, output, "powercfg 执行超时。");
            }

            return new PowerCfgResult(process.ExitCode == 0, process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            Logger.Error($"powercfg {arguments} 执行异常。", ex);
            return new PowerCfgResult(false, -1, string.Empty, ex.Message);
        }
    }

    private sealed record PowerCfgResult(bool Success, int ExitCode, string Output, string Error);
}
