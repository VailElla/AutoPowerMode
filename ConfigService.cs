using System.Text.Json;

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
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                         ?? throw new InvalidDataException("配置文件为空或格式无效。");

            config.Normalize();
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

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
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
}
