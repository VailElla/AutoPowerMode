namespace AutoPowerMode;

public sealed class PowerPlan
{
    public string Guid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string DisplayName
    {
        get
        {
            var activeSuffix = IsActive ? " *" : string.Empty;
            return $"{Name} ({Guid}){activeSuffix}";
        }
    }

    public override string ToString() => DisplayName;
}
