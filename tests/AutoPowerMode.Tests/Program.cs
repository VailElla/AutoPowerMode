using AutoPowerMode;
using System.Drawing;

var tests = new (string Name, Action Run)[]
{
    ("ConfigService migrates legacy idle minutes to seconds", ConfigServiceTests.MigratesLegacyIdleMinutesToSeconds),
    ("ConfigService keeps current idle seconds over legacy minutes", ConfigServiceTests.KeepsCurrentIdleSecondsOverLegacyMinutes),
    ("ConfigService defaults independent check intervals", ConfigServiceTests.DefaultsIndependentCheckIntervals),
    ("ConfigService migrates legacy check intervals", ConfigServiceTests.MigratesLegacyCheckIntervals),
    ("ConfigService preserves independent check intervals", ConfigServiceTests.PreservesIndependentCheckIntervals),
    ("ConfigService defaults AutoStart to false", ConfigServiceTests.DefaultsAutoStartToFalse),
    ("ConfigService defaults notifications to enabled", ConfigServiceTests.DefaultsNotificationsToEnabled),
    ("ConfigService preserves disabled notifications", ConfigServiceTests.PreservesDisabledNotifications),
    ("ConfigService defaults idle protections to disabled", ConfigServiceTests.DefaultsIdleProtectionsToDisabled),
    ("ConfigService preserves enabled idle protections", ConfigServiceTests.PreservesEnabledIdleProtections),
    ("ConfigService defaults language to system", ConfigServiceTests.DefaultsLanguageToSystem),
    ("ConfigService preserves manual language", ConfigServiceTests.PreservesManualLanguage),
    ("Localization uses Chinese for Chinese system cultures", LocalizationTests.UsesChineseForChineseSystemCultures),
    ("Localization uses English for non-Chinese system cultures", LocalizationTests.UsesEnglishForNonChineseSystemCultures),
    ("Localization manual selection overrides system culture", LocalizationTests.ManualSelectionOverridesSystemCulture),
    ("Localization resources contain matching keys", LocalizationTests.ResourcesContainMatchingKeys),
    ("PowerPlanManager parses English powercfg output", PowerPlanManagerTests.ParsesEnglishOutput),
    ("PowerPlanManager parses Simplified Chinese powercfg output", PowerPlanManagerTests.ParsesSimplifiedChineseOutput),
    ("PowerPlanManager parses Traditional Chinese powercfg output", PowerPlanManagerTests.ParsesTraditionalChineseOutput),
    ("PowerPlanManager parses OEM and parenthesized plan names", PowerPlanManagerTests.ParsesOemAndParenthesizedNames),
    ("PowerPlanManager keeps GUID when plan name is missing", PowerPlanManagerTests.KeepsGuidWhenPlanNameIsMissing),
    ("PowerPlanManager does not invent missing standard plans", PowerPlanManagerTests.DoesNotInventMissingStandardPlans),
    ("PowerPlanManager matches GUIDs case-insensitively", PowerPlanManagerTests.MatchesGuidsCaseInsensitively),
    ("PowerModeTransitionPolicy requires consecutive idle confirmations", PowerModeTransitionPolicyTests.RequiresConsecutiveIdleConfirmations),
    ("PowerModeTransitionPolicy resumes active immediately", PowerModeTransitionPolicyTests.ResumesActiveImmediately),
    ("PowerModeTransitionPolicy cancels pending idle confirmations when protected", PowerModeTransitionPolicyTests.ProtectionCancelsPendingIdleConfirmations),
    ("MonitoringIntervalPolicy uses configured active and idle intervals", MonitoringIntervalPolicyTests.UsesConfiguredIntervals),
    ("SystemIdleProtectionDetector recognizes blocking execution-state flags", SystemIdleProtectionDetectorTests.RecognizesBlockingExecutionStateFlags),
    ("SystemIdleProtectionDetector recognizes fullscreen monitor bounds", SystemIdleProtectionDetectorTests.RecognizesFullscreenMonitorBounds),
    ("SystemIdleProtectionDetector excludes Windows shell surfaces", SystemIdleProtectionDetectorTests.ExcludesWindowsShellSurfaces),
    ("DpiLayoutPolicy scales from one hundred to two hundred fifty percent", DpiLayoutPolicyTests.ScalesAcrossSupportedDpiRange),
    ("DpiLayoutPolicy clamps to the monitor working area", DpiLayoutPolicyTests.ClampsToWorkingArea),
    ("DpiLayoutPolicy fits initial size to preferred content", DpiLayoutPolicyTests.FitsInitialSizeToPreferredContent),
    ("PowerPlanNotificationPolicy classifies startup synchronization", PowerPlanNotificationPolicyTests.ClassifiesStartupSynchronization),
    ("PowerPlanNotificationPolicy detects external configured-plan changes", PowerPlanNotificationPolicyTests.DetectsExternalConfiguredPlanChanges),
    ("PowerPlanNotificationPolicy does not misclassify manual no-op", PowerPlanNotificationPolicyTests.DoesNotMisclassifyManualNoOp),
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
    public static void MigratesLegacyIdleMinutesToSeconds()
    {
        var config = ConfigService.Deserialize(
            """
            {
              "idleThresholdMinutes": 15
            }
            """);

        Assert.Equal(900, config.IdleThresholdSeconds);
    }

    public static void KeepsCurrentIdleSecondsOverLegacyMinutes()
    {
        var config = ConfigService.Deserialize(
            """
            {
              "idleThresholdSeconds": 45,
              "idleThresholdMinutes": 15
            }
            """);

        Assert.Equal(45, config.IdleThresholdSeconds);
    }

    public static void DefaultsIndependentCheckIntervals()
    {
        var config = ConfigService.Deserialize("{}");
        var serialized = ConfigService.Serialize(config);
        var clone = config.Clone();

        Assert.Equal(30, config.ActiveCheckIntervalSeconds);
        Assert.Equal(1, config.IdleCheckIntervalSeconds);
        Assert.Contains("\"activeCheckIntervalSeconds\": 30", serialized);
        Assert.Contains("\"idleCheckIntervalSeconds\": 1", serialized);
        Assert.Equal(30, clone.ActiveCheckIntervalSeconds);
        Assert.Equal(1, clone.IdleCheckIntervalSeconds);
    }

    public static void MigratesLegacyCheckIntervals()
    {
        var config = ConfigService.Deserialize(
            """
            {
              "checkIntervalSeconds": 6,
              "checkIntervalMinutes": 2
            }
            """);

        var serialized = ConfigService.Serialize(config);

        Assert.Equal(6, config.ActiveCheckIntervalSeconds);
        Assert.Equal(1, config.IdleCheckIntervalSeconds);
        Assert.Contains("\"activeCheckIntervalSeconds\": 6", serialized);
        Assert.Contains("\"idleCheckIntervalSeconds\": 1", serialized);
        Assert.DoesNotContain("checkIntervalSeconds", serialized);
        Assert.DoesNotContain("checkIntervalMinutes", serialized);

        var legacyMinutes = ConfigService.Deserialize("{ \"checkIntervalMinutes\": 2 }");
        Assert.Equal(60, legacyMinutes.ActiveCheckIntervalSeconds);
    }

    public static void PreservesIndependentCheckIntervals()
    {
        var config = ConfigService.Deserialize(
            """
            {
              "activeCheckIntervalSeconds": 45,
              "idleCheckIntervalSeconds": 2,
              "checkIntervalSeconds": 6
            }
            """);

        var serialized = ConfigService.Serialize(config);
        var clone = config.Clone();

        Assert.Equal(45, config.ActiveCheckIntervalSeconds);
        Assert.Equal(2, config.IdleCheckIntervalSeconds);
        Assert.Contains("\"activeCheckIntervalSeconds\": 45", serialized);
        Assert.Contains("\"idleCheckIntervalSeconds\": 2", serialized);
        Assert.DoesNotContain("checkIntervalSeconds", serialized);
        Assert.Equal(45, clone.ActiveCheckIntervalSeconds);
        Assert.Equal(2, clone.IdleCheckIntervalSeconds);

        var normalized = ConfigService.Deserialize(
            "{ \"activeCheckIntervalSeconds\": 90, \"idleCheckIntervalSeconds\": 0 }");
        Assert.Equal(60, normalized.ActiveCheckIntervalSeconds);
        Assert.Equal(1, normalized.IdleCheckIntervalSeconds);
    }

    public static void DefaultsAutoStartToFalse()
    {
        var config = ConfigService.Deserialize("{}");

        Assert.False(config.AutoStart);
    }

    public static void DefaultsNotificationsToEnabled()
    {
        var config = ConfigService.Deserialize("{}");

        Assert.True(config.NotificationsEnabled);
    }

    public static void PreservesDisabledNotifications()
    {
        var config = ConfigService.Deserialize(
            """
            {
              "notificationsEnabled": false
            }
            """);

        Assert.False(config.NotificationsEnabled);
        Assert.Contains("\"notificationsEnabled\": false", ConfigService.Serialize(config));
        Assert.False(config.Clone().NotificationsEnabled);
    }

    public static void DefaultsIdleProtectionsToDisabled()
    {
        var config = ConfigService.Deserialize("{}");

        Assert.False(config.PreventIdleOnExecutionState);
        Assert.False(config.PreventIdleOnFullscreen);
    }

    public static void PreservesEnabledIdleProtections()
    {
        var config = ConfigService.Deserialize(
            """
            {
              "preventIdleOnExecutionState": true,
              "preventIdleOnFullscreen": true
            }
            """);

        var serialized = ConfigService.Serialize(config);
        var clone = config.Clone();

        Assert.True(config.PreventIdleOnExecutionState);
        Assert.True(config.PreventIdleOnFullscreen);
        Assert.Contains("\"preventIdleOnExecutionState\": true", serialized);
        Assert.Contains("\"preventIdleOnFullscreen\": true", serialized);
        Assert.True(clone.PreventIdleOnExecutionState);
        Assert.True(clone.PreventIdleOnFullscreen);
    }

    public static void DefaultsLanguageToSystem()
    {
        var config = ConfigService.Deserialize("{}");

        Assert.Equal(AppLanguagePreference.System, config.Language);
    }

    public static void PreservesManualLanguage()
    {
        var config = ConfigService.Deserialize("{ \"language\": \"en\" }");

        Assert.Equal(AppLanguagePreference.English, config.Language);
        Assert.Contains("\"language\": \"en\"", ConfigService.Serialize(config));
        Assert.Equal(AppLanguagePreference.English, config.Clone().Language);

        var invalid = ConfigService.Deserialize("{ \"language\": \"fr\" }");
        Assert.Equal(AppLanguagePreference.System, invalid.Language);
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

internal static class LocalizationTests
{
    public static void UsesChineseForChineseSystemCultures()
    {
        Assert.Equal(
            AppLanguage.SimplifiedChinese,
            LocalizationService.ResolveLanguage(AppLanguagePreference.System, new System.Globalization.CultureInfo("zh-CN")));
        Assert.Equal(
            AppLanguage.SimplifiedChinese,
            LocalizationService.ResolveLanguage(AppLanguagePreference.System, new System.Globalization.CultureInfo("zh-TW")));
    }

    public static void UsesEnglishForNonChineseSystemCultures()
    {
        Assert.Equal(
            AppLanguage.English,
            LocalizationService.ResolveLanguage(AppLanguagePreference.System, new System.Globalization.CultureInfo("en-US")));
        Assert.Equal(
            AppLanguage.English,
            LocalizationService.ResolveLanguage(AppLanguagePreference.System, new System.Globalization.CultureInfo("ja-JP")));
    }

    public static void ManualSelectionOverridesSystemCulture()
    {
        Assert.Equal(
            AppLanguage.English,
            LocalizationService.ResolveLanguage(AppLanguagePreference.English, new System.Globalization.CultureInfo("zh-CN")));
        Assert.Equal(
            AppLanguage.SimplifiedChinese,
            LocalizationService.ResolveLanguage(AppLanguagePreference.SimplifiedChinese, new System.Globalization.CultureInfo("en-US")));
    }

    public static void ResourcesContainMatchingKeys()
    {
        Assert.True(LocalizationService.ResourceKeysMatch);
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

    public static void ProtectionCancelsPendingIdleConfirmations()
    {
        var policy = new PowerModeTransitionPolicy(requiredIdleDetections: 2);
        policy.MarkActivityState(UserActivityState.Active);

        var firstIdleDetection = policy.Evaluate(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10));
        var protectedResult = policy.SuppressIdleTransition();
        var firstDetectionAfterProtection = policy.Evaluate(
            TimeSpan.FromSeconds(21),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10));
        var secondDetectionAfterProtection = policy.Evaluate(
            TimeSpan.FromSeconds(22),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10));

        Assert.Null(firstIdleDetection);
        Assert.Null(protectedResult);
        Assert.Null(firstDetectionAfterProtection);
        Assert.Equal(UserActivityState.Idle, secondDetectionAfterProtection);

        policy.MarkActivityState(UserActivityState.Idle);
        Assert.Equal(UserActivityState.Active, policy.SuppressIdleTransition());
    }
}

internal static class MonitoringIntervalPolicyTests
{
    public static void UsesConfiguredIntervals()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), MonitoringIntervalPolicy.GetInterval(UserActivityState.Unknown, 30, 1));
        Assert.Equal(TimeSpan.FromSeconds(12), MonitoringIntervalPolicy.GetInterval(UserActivityState.Active, 12, 3));
        Assert.Equal(TimeSpan.FromSeconds(3), MonitoringIntervalPolicy.GetInterval(UserActivityState.Idle, 12, 3));
        Assert.Equal(TimeSpan.FromSeconds(1), MonitoringIntervalPolicy.GetInterval(UserActivityState.Active, 0, 3));
        Assert.Equal(TimeSpan.FromSeconds(60), MonitoringIntervalPolicy.GetInterval(UserActivityState.Idle, 12, 90));
    }
}

internal static class SystemIdleProtectionDetectorTests
{
    public static void RecognizesBlockingExecutionStateFlags()
    {
        Assert.False(SystemIdleProtectionDetector.HasBlockingExecutionState(0));
        Assert.True(SystemIdleProtectionDetector.HasBlockingExecutionState(0x00000001));
        Assert.True(SystemIdleProtectionDetector.HasBlockingExecutionState(0x00000002));
        Assert.True(SystemIdleProtectionDetector.HasBlockingExecutionState(0x00000040));
        Assert.True(SystemIdleProtectionDetector.HasBlockingExecutionState(0x00000043));
        Assert.False(SystemIdleProtectionDetector.HasBlockingExecutionState(0x80000000));
        Assert.False(SystemIdleProtectionDetector.HasBlockingExecutionState(0x00000004));
    }

    public static void RecognizesFullscreenMonitorBounds()
    {
        var monitor = new SystemIdleProtectionDetector.NativeRect(0, 0, 1920, 1080);
        var exact = new SystemIdleProtectionDetector.NativeRect(0, 0, 1920, 1080);
        var withinTolerance = new SystemIdleProtectionDetector.NativeRect(-2, 1, 1922, 1078);
        var maximizedWorkArea = new SystemIdleProtectionDetector.NativeRect(0, 0, 1920, 1040);

        Assert.True(SystemIdleProtectionDetector.CoversMonitorBounds(exact, monitor));
        Assert.True(SystemIdleProtectionDetector.CoversMonitorBounds(withinTolerance, monitor));
        Assert.False(SystemIdleProtectionDetector.CoversMonitorBounds(maximizedWorkArea, monitor));
    }

    public static void ExcludesWindowsShellSurfaces()
    {
        Assert.True(SystemIdleProtectionDetector.IsExcludedShellWindowClass("Progman"));
        Assert.True(SystemIdleProtectionDetector.IsExcludedShellWindowClass("WorkerW"));
        Assert.True(SystemIdleProtectionDetector.IsExcludedShellWindowClass("Shell_TrayWnd"));
        Assert.True(SystemIdleProtectionDetector.IsExcludedShellWindowClass("shell_secondarytraywnd"));
        Assert.False(SystemIdleProtectionDetector.IsExcludedShellWindowClass("Chrome_WidgetWin_1"));
        Assert.False(SystemIdleProtectionDetector.IsExcludedShellWindowClass(null));
    }
}

internal static class DpiLayoutPolicyTests
{
    public static void ScalesAcrossSupportedDpiRange()
    {
        var supportedDpis = new[] { 96, 120, 144, 168, 192, 216, 240 };

        foreach (var dpi in supportedDpis)
        {
            var metrics = DpiLayoutPolicy.Calculate(
                dpi,
                new Size(3840, 2160),
                Size.Empty);

            Assert.Equal(DpiLayoutPolicy.Scale(DpiLayoutPolicy.InitialClientWidth, dpi), metrics.InitialClientSize.Width);
            Assert.Equal(DpiLayoutPolicy.Scale(DpiLayoutPolicy.InitialClientHeight, dpi), metrics.InitialClientSize.Height);
            Assert.Equal(DpiLayoutPolicy.Scale(DpiLayoutPolicy.MinimumClientWidth, dpi), metrics.MinimumClientSize.Width);
            Assert.Equal(DpiLayoutPolicy.Scale(DpiLayoutPolicy.MinimumClientHeight, dpi), metrics.MinimumClientSize.Height);
            Assert.True(metrics.InitialClientSize.Width <= metrics.MaximumClientSize.Width);
            Assert.True(metrics.InitialClientSize.Height <= metrics.MaximumClientSize.Height);
        }
    }

    public static void ClampsToWorkingArea()
    {
        var metrics = DpiLayoutPolicy.Calculate(
            dpi: 240,
            workingArea: new Size(800, 600),
            nonClientSize: new Size(40, 100));

        Assert.Equal(new Size(700, 440), metrics.MaximumClientSize);
        Assert.Equal(metrics.MaximumClientSize, metrics.MinimumClientSize);
        Assert.Equal(metrics.MaximumClientSize, metrics.InitialClientSize);
    }

    public static void FitsInitialSizeToPreferredContent()
    {
        var metrics = new DpiLayoutMetrics(
            new Size(460, 270),
            new Size(400, 240),
            new Size(800, 600));

        Assert.Equal(
            new Size(520, 390),
            DpiLayoutPolicy.FitInitialClientSize(metrics, new Size(520, 390)));
        Assert.Equal(
            metrics.InitialClientSize,
            DpiLayoutPolicy.FitInitialClientSize(metrics, new Size(320, 180)));
        Assert.Equal(
            metrics.MaximumClientSize,
            DpiLayoutPolicy.FitInitialClientSize(metrics, new Size(1200, 900)));
    }
}

internal static class PowerPlanNotificationPolicyTests
{
    public static void ClassifiesStartupSynchronization()
    {
        var disposition = PowerPlanNotificationPolicy.ClassifyAlreadyActivePlan(
            UserActivityState.Unknown,
            UserActivityState.Active,
            manual: false);

        Assert.Equal(AlreadyActivePlanDisposition.StartupSynchronized, disposition);
    }

    public static void DetectsExternalConfiguredPlanChanges()
    {
        var disposition = PowerPlanNotificationPolicy.ClassifyAlreadyActivePlan(
            UserActivityState.Idle,
            UserActivityState.Active,
            manual: false);

        Assert.Equal(AlreadyActivePlanDisposition.ExternalChangeDetected, disposition);
    }

    public static void DoesNotMisclassifyManualNoOp()
    {
        var disposition = PowerPlanNotificationPolicy.ClassifyAlreadyActivePlan(
            UserActivityState.Idle,
            UserActivityState.Active,
            manual: true);

        Assert.Equal(AlreadyActivePlanDisposition.NoChange, disposition);
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
