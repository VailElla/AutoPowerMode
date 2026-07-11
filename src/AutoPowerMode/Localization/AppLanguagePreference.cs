namespace AutoPowerMode;

public static class AppLanguagePreference
{
    public const string System = "system";
    public const string English = "en";
    public const string SimplifiedChinese = "zh-CN";

    public static string Normalize(string? preference)
    {
        return preference switch
        {
            English => English,
            SimplifiedChinese => SimplifiedChinese,
            _ => System
        };
    }
}
