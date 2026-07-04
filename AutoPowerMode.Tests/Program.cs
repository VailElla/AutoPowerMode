using AutoPowerMode;

var tests = new (string Name, Action Run)[]
{
    ("ConfigService migrates legacy minutes to seconds", ConfigServiceTests.MigratesLegacyMinutesToSeconds),
    ("ConfigService keeps current second fields over legacy fields", ConfigServiceTests.KeepsSecondFieldsOverLegacyFields),
    ("ConfigService defaults AutoStart to false", ConfigServiceTests.DefaultsAutoStartToFalse),
    ("PowerPlanManager parses English powercfg output", PowerPlanManagerTests.ParsesEnglishOutput),
    ("PowerPlanManager parses Simplified Chinese powercfg output", PowerPlanManagerTests.ParsesSimplifiedChineseOutput),
    ("PowerPlanManager parses Traditional Chinese powercfg output", PowerPlanManagerTests.ParsesTraditionalChineseOutput),
    ("PowerPlanManager parses OEM and parenthesized plan names", PowerPlanManagerTests.ParsesOemAndParenthesizedNames),
    ("PowerPlanManager keeps GUID when plan name is missing", PowerPlanManagerTests.KeepsGuidWhenPlanNameIsMissing),
    ("PowerPlanManager does not invent missing standard plans", PowerPlanManagerTests.DoesNotInventMissingStandardPlans),
    ("PowerPlanManager matches GUIDs case-insensitively", PowerPlanManagerTests.MatchesGuidsCaseInsensitively),
    ("PowerModeTransitionPolicy requires consecutive idle confirmations", PowerModeTransitionPolicyTests.RequiresConsecutiveIdleConfirmations),
    ("PowerModeTransitionPolicy resumes active immediately", PowerModeTransitionPolicyTests.ResumesActiveImmediately),
    ("ConfigService cleans stale temp files only", ConfigServiceTests.CleansStaleTempFilesOnly),
    ("Logger rotates app.log after one megabyte", LoggerTests.RotatesAfterOneMegabyte),
    ("Logger keeps at most three archived logs", LoggerTests.KeepsAtMostThreeArchives),
    ("Logger sanitizes AppData and app directory paths", LoggerTests.SanitizesAppDataAndAppDirectoryPaths),
    ("Logger append failures do not throw", LoggerTests.AppendFailuresDoNotThrow),
    ("StartupService detects only the current executable path", StartupServiceTests.DetectsOnlyCurrentExecutablePath),
    ("StartupService quotes paths consistently", StartupServiceTests.QuotesPathsConsistently),
    ("PowerPlanOverridePolicy skips automatic override of external custom plans", PowerPlanOverridePolicyTests.SkipsAutomaticOverrideOfExternalCustomPlans)
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine(ex.Message);
    }
}

if (failed > 0)
{
    Console.Error.WriteLine($"{failed} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine($"{tests.Length} test(s) passed.");

internal static class ConfigServiceTests
{
    public static void MigratesLegacyMinutesToSeconds()
    {
        var config = ConfigService.Deserialize(
            """
            {
              "idleThresholdMinutes": 15,
              "checkIntervalMinutes": 2
            }
            """);

        Assert.Equal(900, config.IdleThresholdSeconds);
        Assert.Equal(120, config.CheckIntervalSeconds);
    }

    public static void KeepsSecondFieldsOverLegacyFields()
    {
        var config = ConfigService.Deserialize(
            """
            {
              "idleThresholdSeconds": 45,
              "idleThresholdMinutes": 15,
              "checkIntervalSeconds": 6,
              "checkIntervalMinutes": 2
            }
            """);

        Assert.Equal(45, config.IdleThresholdSeconds);
        Assert.Equal(6, config.CheckIntervalSeconds);
    }

    public static void DefaultsAutoStartToFalse()
    {
        var config = ConfigService.Deserialize("{}");

        Assert.False(config.AutoStart);
    }

    public static void CleansStaleTempFilesOnly()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var staleTempPath = Path.Combine(tempDirectory.Path, "config.123.abcdef.tmp");
        var freshTempPath = Path.Combine(tempDirectory.Path, "config.456.abcdef.tmp");
        var unrelatedPath = Path.Combine(tempDirectory.Path, "config.keep.json");
        File.WriteAllText(staleTempPath, "stale");
        File.WriteAllText(freshTempPath, "fresh");
        File.WriteAllText(unrelatedPath, "keep");

        var now = DateTimeOffset.UtcNow;
        File.SetLastWriteTimeUtc(staleTempPath, now.AddDays(-2).UtcDateTime);
        File.SetLastWriteTimeUtc(freshTempPath, now.AddHours(-2).UtcDateTime);

        var deletedCount = ConfigService.CleanupStaleTempFiles(tempDirectory.Path, now);

        Assert.Equal(1, deletedCount);
        Assert.False(File.Exists(staleTempPath));
        Assert.True(File.Exists(freshTempPath));
        Assert.True(File.Exists(unrelatedPath));
    }
}

internal static class PowerPlanManagerTests
{
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string PowerSaverGuid = "a1841308-3541-4fab-bc81-f71556f20b4a";
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string OemGuid = "11111111-2222-3333-4444-555555555555";
    private const string GamingGuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    public static void ParsesEnglishOutput()
    {
        var plans = PowerPlanManager.ParsePlans(
            $"""
            Existing Power Schemes (* Active)
            -----------------------------------
            Power Scheme GUID: {BalancedGuid}  (Balanced) *
            Power Scheme GUID: {PowerSaverGuid}  (Power Saver)
            Power Scheme GUID: {HighPerformanceGuid}  (High Performance)
            """);

        Assert.Equal(3, plans.Count);
        Assert.Equal("Balanced", plans[0].Name);
        Assert.True(plans[0].IsActive);
        Assert.Equal("Power Saver", plans[1].Name);
        Assert.Equal("High Performance", plans[2].Name);
    }

    public static void ParsesSimplifiedChineseOutput()
    {
        var plans = PowerPlanManager.ParsePlans(
            $"""
            现有电源方案 (* 活动)
            电源方案 GUID: {BalancedGuid}  (平衡) *
            电源方案 GUID: {PowerSaverGuid}  (节能)
            电源方案 GUID: {HighPerformanceGuid}  (高性能)
            """);

        Assert.Equal(3, plans.Count);
        Assert.Equal("平衡", plans[0].Name);
        Assert.True(plans[0].IsActive);
        Assert.Equal("节能", plans[1].Name);
        Assert.Equal("高性能", plans[2].Name);
    }

    public static void ParsesTraditionalChineseOutput()
    {
        var plans = PowerPlanManager.ParsePlans(
            $"""
            現有電源配置 (* 作用中)
            電源配置 GUID: {BalancedGuid}  (平衡) *
            電源配置 GUID: {PowerSaverGuid}  (節能)
            電源配置 GUID: {HighPerformanceGuid}  (高效能)
            """);

        Assert.Equal(3, plans.Count);
        Assert.Equal("平衡", plans[0].Name);
        Assert.True(plans[0].IsActive);
        Assert.Equal("節能", plans[1].Name);
        Assert.Equal("高效能", plans[2].Name);
    }

    public static void ParsesOemAndParenthesizedNames()
    {
        var plans = PowerPlanManager.ParsePlans(
            $"""
            Power Scheme GUID: {OemGuid}  (ASUS Recommended)
            Power Scheme GUID: {GamingGuid}  (My Plan (Gaming)) *
            """);

        Assert.Equal(2, plans.Count);
        Assert.Equal("ASUS Recommended", plans[0].Name);
        Assert.Equal("My Plan (Gaming)", plans[1].Name);
        Assert.True(plans[1].IsActive);
    }

    public static void KeepsGuidWhenPlanNameIsMissing()
    {
        var plans = PowerPlanManager.ParsePlans(
            """
            Power Scheme GUID: 12345678-1234-1234-1234-123456789abc
            """);

        Assert.Equal(1, plans.Count);
        Assert.Equal("12345678-1234-1234-1234-123456789abc", plans[0].Guid);
        Assert.Equal("Unknown Power Plan (12345678)", plans[0].Name);
        Assert.False(plans[0].IsActive);
    }

    public static void DoesNotInventMissingStandardPlans()
    {
        var manager = new PowerPlanManager();
        var config = AppConfig.CreateDefault();
        var plans = new List<PowerPlan>
        {
            new() { Guid = BalancedGuid, Name = "Balanced", IsActive = true },
            new() { Guid = OemGuid, Name = "Dell Optimized" }
        };

        var changed = manager.TryAutoConfigure(config, plans);

        Assert.False(changed);
        Assert.Equal(string.Empty, config.ActivePowerPlanGuid);
        Assert.Equal(string.Empty, config.IdlePowerPlanGuid);
    }

    public static void MatchesGuidsCaseInsensitively()
    {
        var manager = new PowerPlanManager();
        var plans = new List<PowerPlan>
        {
            new() { Guid = HighPerformanceGuid.ToUpperInvariant(), Name = "High Performance" }
        };

        var matched = manager.FindByGuid(plans, $"  {HighPerformanceGuid.ToLowerInvariant()}  ");

        Assert.NotNull(matched);
    }
}

internal static class PowerModeTransitionPolicyTests
{
    public static void RequiresConsecutiveIdleConfirmations()
    {
        var policy = new PowerModeTransitionPolicy(requiredIdleDetections: 2);
        policy.MarkActivityState(UserActivityState.Active);

        var first = policy.Evaluate(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10));

        var second = policy.Evaluate(
            TimeSpan.FromSeconds(21),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10));

        Assert.Null(first);
        Assert.Equal(UserActivityState.Idle, second);
    }

    public static void ResumesActiveImmediately()
    {
        var policy = new PowerModeTransitionPolicy(requiredIdleDetections: 2);
        policy.MarkActivityState(UserActivityState.Idle);

        var result = policy.Evaluate(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10));

        Assert.Equal(UserActivityState.Active, result);
    }
}

internal static class LoggerTests
{
    public static void RotatesAfterOneMegabyte()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var logPath = Path.Combine(tempDirectory.Path, "app.log");
        File.WriteAllText(logPath, new string('a', (int)Logger.MaxLogFileBytes));

        Logger.TryAppend(tempDirectory.Path, logPath, "new line" + Environment.NewLine);

        Assert.True(File.Exists(logPath));
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "app.1.log")));
        Assert.Equal("new line" + Environment.NewLine, File.ReadAllText(logPath));
        Assert.Equal(Logger.MaxLogFileBytes, new FileInfo(Path.Combine(tempDirectory.Path, "app.1.log")).Length);
    }

    public static void KeepsAtMostThreeArchives()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var logPath = Path.Combine(tempDirectory.Path, "app.log");
        File.WriteAllText(logPath, new string('a', (int)Logger.MaxLogFileBytes));
        File.WriteAllText(Path.Combine(tempDirectory.Path, "app.1.log"), "one");
        File.WriteAllText(Path.Combine(tempDirectory.Path, "app.2.log"), "two");
        File.WriteAllText(Path.Combine(tempDirectory.Path, "app.3.log"), "three");

        Logger.TryAppend(tempDirectory.Path, logPath, "new line" + Environment.NewLine);

        Assert.True(File.Exists(logPath));
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "app.1.log")));
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "app.2.log")));
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "app.3.log")));
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, "app.4.log")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(tempDirectory.Path, "app.3.log")));
    }

    public static void SanitizesAppDataAndAppDirectoryPaths()
    {
        var message = string.Join(
            " | ",
            @"C:\Users\Ella\AppData\Roaming\AutoPowerMode\logs\app.log",
            Path.Combine(Logger.AppDataDirectory, "logs", "app.log"),
            Path.Combine(AppContext.BaseDirectory, "logs", "app.log"));

        var sanitized = Logger.SanitizeMessage(message);

        Assert.Contains(@"%AppData%\AutoPowerMode", sanitized);
        Assert.Contains("<AppDirectory>", sanitized);
        Assert.DoesNotContain(@"C:\Users\Ella\AppData\Roaming\AutoPowerMode", sanitized);
        Assert.DoesNotContain(Logger.AppDataDirectory, sanitized);
        Assert.DoesNotContain(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), sanitized);
    }

    public static void AppendFailuresDoNotThrow()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"autopowermode-test-{Guid.NewGuid():N}");
        File.WriteAllText(tempFile, "not a directory");

        try
        {
            Logger.TryAppend(tempFile, Path.Combine(tempFile, "app.log"), "line");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

internal static class StartupServiceTests
{
    public static void DetectsOnlyCurrentExecutablePath()
    {
        const string currentPath = @"C:\Program Files\AutoPowerMode\AutoPowerMode.exe";

        Assert.True(StartupRegistrationValue.IsEnabledValue(
            @"""C:\Program Files\AutoPowerMode\AutoPowerMode.exe""",
            currentPath));
        Assert.True(StartupRegistrationValue.IsEnabledValue(
            @"""c:\program files\autopowermode\autopowermode.exe""",
            currentPath));
        Assert.False(StartupRegistrationValue.IsEnabledValue(
            @"""C:\Old\AutoPowerMode.exe""",
            currentPath));
        Assert.False(StartupRegistrationValue.IsEnabledValue(
            string.Empty,
            currentPath));
    }

    public static void QuotesPathsConsistently()
    {
        Assert.Equal(
            @"""C:\Program Files\AutoPowerMode\AutoPowerMode.exe""",
            StartupRegistrationValue.QuotePath(@" ""C:\Program Files\AutoPowerMode\AutoPowerMode.exe"" "));
    }
}

internal static class PowerPlanOverridePolicyTests
{
    public static void SkipsAutomaticOverrideOfExternalCustomPlans()
    {
        var config = new AppConfig
        {
            ActivePowerPlanGuid = "active-guid",
            IdlePowerPlanGuid = "idle-guid"
        };

        Assert.True(PowerPlanOverridePolicy.ShouldSkipAutomaticSwitch(
            manual: false,
            currentPlanGuid: "custom-guid",
            config));
        Assert.False(PowerPlanOverridePolicy.ShouldSkipAutomaticSwitch(
            manual: false,
            currentPlanGuid: "active-guid",
            config));
        Assert.False(PowerPlanOverridePolicy.ShouldSkipAutomaticSwitch(
            manual: false,
            currentPlanGuid: "idle-guid",
            config));
        Assert.False(PowerPlanOverridePolicy.ShouldSkipAutomaticSwitch(
            manual: true,
            currentPlanGuid: "custom-guid",
            config));
    }
}

internal static class Assert
{
    public static void True(bool condition, string? message = null)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message ?? "Expected true.");
        }
    }

    public static void False(bool condition, string? message = null)
    {
        if (condition)
        {
            throw new InvalidOperationException(message ?? "Expected false.");
        }
    }

    public static void Null(object? value)
    {
        if (value is not null)
        {
            throw new InvalidOperationException($"Expected null, got {value}.");
        }
    }

    public static void NotNull(object? value)
    {
        if (value is null)
        {
            throw new InvalidOperationException("Expected non-null value.");
        }
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    public static void Contains(string expectedSubstring, string actual)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected to find '{expectedSubstring}' in '{actual}'.");
        }
    }

    public static void DoesNotContain(string unexpectedSubstring, string actual)
    {
        if (actual.Contains(unexpectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Did not expect to find '{unexpectedSubstring}' in '{actual}'.");
        }
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    private TemporaryDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TemporaryDirectory Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"autopowermode-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path);
    }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
