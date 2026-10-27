namespace DashTweaks.Tweaks;

public enum RiskLevel
{
    Safe,
    Moderate
}

public interface ITweak
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Category { get; } // "Network", "GPU", "Memory", "Services"
    RiskLevel Risk { get; }  // Enum: Safe, Moderate
    bool RequiresRestart { get; }
    
    bool Apply();
    bool Revert();
}
