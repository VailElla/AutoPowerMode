using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoPowerMode;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    static ConfigService()
    {
        JsonOptions.Converters.Add(new AppConfigJsonConverter());
    }

    public string ConfigDirectory => Logger.AppDataDirectory;

    public string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public AppConfig Load()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            if (!File.Exists(ConfigPath))
            {
                Logger.Info("配置文件不存在，创建默认配置。");
                var defaultConfig = AppConfig.CreateDefault();
                Save(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigPath);
            var config = Deserialize(json);

            config.Normalize();
            Logger.Info($"已加载配置：空闲阈值={config.IdleThresholdSeconds} 秒，检测间隔={config.CheckIntervalSeconds} 秒，活跃计划={config.ActivePowerPlanGuid}，空闲计划={config.IdlePowerPlanGuid}，用户配置计划={config.PowerPlansConfiguredByUser}，开机自启={config.AutoStart}，暂停={config.IsPaused}");
            return config;
        }
        catch (Exception ex)
        {
            Logger.Error("配置读取失败，准备备份损坏配置并恢复默认配置。", ex);
            BackupCorruptConfig();

            var defaultConfig = AppConfig.CreateDefault();
            Save(defaultConfig);
            return defaultConfig;
        }
    }

    public bool Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            config.Normalize();

            var json = Serialize(config);
            var tempPath = Path.Combine(ConfigDirectory, $"config.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, json);

            if (File.Exists(ConfigPath))
            {
                File.Replace(tempPath, ConfigPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, ConfigPath);
            }

            Logger.Info($"配置已保存到 {ConfigPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("配置保存失败。", ex);
            return false;
        }
    }

    private void BackupCorruptConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupPath = Path.Combine(ConfigDirectory, $"config.corrupt.{timestamp}.json");
            File.Copy(ConfigPath, backupPath, overwrite: true);
            Logger.Info($"损坏配置已备份到 {backupPath}");
        }
        catch (Exception ex)
        {
            Logger.Error("备份损坏配置失败。", ex);
        }
    }

    internal static AppConfig Deserialize(string json)
    {
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                     ?? throw new InvalidDataException("配置文件为空或格式无效。");

        config.Normalize();
        return config;
    }

    internal static string Serialize(AppConfig config)
    {
        config.Normalize();
        return JsonSerializer.Serialize(config, JsonOptions);
    }

    internal static int ConvertLegacyMinutesToSeconds(int minutes)
    {
        if (minutes <= 0)
        {
            return 0;
        }

        var seconds = (long)minutes * 60;
        return seconds > int.MaxValue ? int.MaxValue : (int)seconds;
    }

    private sealed class AppConfigJsonConverter : JsonConverter<AppConfig>
    {
        public override AppConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            var config = AppConfig.CreateDefault();

            if (root.TryGetProperty("idleThresholdSeconds", out var idleThresholdSecondsElement) && idleThresholdSecondsElement.TryGetInt32(out var idleThresholdSeconds))
            {
                config.IdleThresholdSeconds = idleThresholdSeconds;
            }
            else if (root.TryGetProperty("idleThresholdMinutes", out var idleThresholdMinutesElement) && idleThresholdMinutesElement.TryGetInt32(out var idleThresholdMinutes))
            {
                config.LegacyIdleThresholdMinutes = idleThresholdMinutes;
                config.IdleThresholdSeconds = ConvertLegacyMinutesToSeconds(idleThresholdMinutes);
                Logger.Info($"检测到旧版 idleThresholdMinutes={idleThresholdMinutes}，已迁移为 idleThresholdSeconds={config.IdleThresholdSeconds}。");
            }

            if (root.TryGetProperty("checkIntervalSeconds", out var checkSeconds) && checkSeconds.TryGetInt32(out var checkIntervalSeconds))
            {
                config.CheckIntervalSeconds = checkIntervalSeconds;
            }
            else if (root.TryGetProperty("checkIntervalMinutes", out var legacyCheckMinutes) && legacyCheckMinutes.TryGetInt32(out var checkIntervalMinutes))
            {
                config.LegacyCheckIntervalMinutes = checkIntervalMinutes;
                config.CheckIntervalSeconds = ConvertLegacyMinutesToSeconds(checkIntervalMinutes);
                Logger.Info($"检测到旧版 checkIntervalMinutes={checkIntervalMinutes}，已迁移为 checkIntervalSeconds={config.CheckIntervalSeconds}。");
            }

            if (root.TryGetProperty("idlePowerPlanGuid", out var idleGuid))
            {
                config.IdlePowerPlanGuid = idleGuid.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("activePowerPlanGuid", out var activeGuid))
            {
                config.ActivePowerPlanGuid = activeGuid.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("autoStart", out var autoStart) && (autoStart.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                config.AutoStart = autoStart.GetBoolean();
            }

            if (root.TryGetProperty("isPaused", out var isPaused) && (isPaused.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                config.IsPaused = isPaused.GetBoolean();
            }

            if (root.TryGetProperty("powerPlansConfiguredByUser", out var configuredByUser) && (configuredByUser.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                config.PowerPlansConfiguredByUser = configuredByUser.GetBoolean();
            }

            return config;
        }

        public override void Write(Utf8JsonWriter writer, AppConfig value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("idleThresholdSeconds", value.IdleThresholdSeconds);
            writer.WriteNumber("checkIntervalSeconds", value.CheckIntervalSeconds);
            writer.WriteString("idlePowerPlanGuid", value.IdlePowerPlanGuid);
            writer.WriteString("activePowerPlanGuid", value.ActivePowerPlanGuid);
            writer.WriteBoolean("autoStart", value.AutoStart);
            writer.WriteBoolean("isPaused", value.IsPaused);
            writer.WriteBoolean("powerPlansConfiguredByUser", value.PowerPlansConfiguredByUser);
            writer.WriteEndObject();
        }
    }
}
