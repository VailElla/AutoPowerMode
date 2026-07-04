namespace AutoPowerMode;

internal static class StartupRegistrationValue
{
    public static bool IsEnabledValue(string? registryValue, string executablePath)
    {
        var registeredPath = ExtractExecutablePath(registryValue);
        if (string.IsNullOrWhiteSpace(registeredPath) || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        return string.Equals(
            NormalizePathForComparison(registeredPath),
            NormalizePathForComparison(executablePath),
            StringComparison.OrdinalIgnoreCase);
    }

    public static string? ExtractExecutablePath(string? registryValue)
    {
        if (string.IsNullOrWhiteSpace(registryValue))
        {
            return null;
        }

        var trimmed = registryValue.Trim();
        if (trimmed.StartsWith('"'))
        {
            var closingQuoteIndex = trimmed.IndexOf('"', 1);
            return closingQuoteIndex > 1
                ? trimmed[1..closingQuoteIndex]
                : trimmed.Trim('"');
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeIndex >= 0 ? trimmed[..(exeIndex + 4)] : trimmed;
    }

    public static string QuotePath(string path)
    {
        var trimmed = path.Trim().Trim('"');
        return $"\"{trimmed}\"";
    }

    private static string NormalizePathForComparison(string path)
    {
        return path.Trim().Trim('"').Replace('/', '\\');
    }
}
