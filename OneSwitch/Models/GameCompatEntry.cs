namespace OneSwitch.Models;

public enum CompatTier
{
    RunsPerfectly,
    RunsFine,
    // ProtonDB bronze/borked: technically launches (or not), but not an experience
    // worth migrating for. Kept distinct from Unknown so the Slide 3 compatibility
    // percentage isn't flattered by games that effectively don't work.
    WontRun,
    Unknown
}

public record GameCompatEntry(
    string AppId,
    string Title,
    CompatTier Tier,
    string? ProtonDbNote);
