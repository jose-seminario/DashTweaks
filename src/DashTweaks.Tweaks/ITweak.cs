namespace DashTweaks.Tweaks;

public enum RiskLevel
{
    Safe,
    Moderate,
    Aggressive
}

public interface ITweak
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Category { get; } // "Network", "GPU", "Memory", "Services", "Peripherals", "Power", "Processes"
    RiskLevel Risk { get; }  // Enum: Safe, Moderate, Aggressive
    bool RequiresRestart { get; }
    
    bool Apply();
    bool Revert();
}
