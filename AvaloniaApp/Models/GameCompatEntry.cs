namespace AvaloniaApp.Models;

public enum CompatTier
{
    RunsPerfectly,
    RunsFine,
    Unknown
}

public record GameCompatEntry(
    string AppId,
    string Title,
    CompatTier Tier,
    string? ProtonDbNote);
