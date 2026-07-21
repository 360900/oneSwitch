using System;
using System.Collections.Generic;
using AvaloniaApp.Models;

namespace AvaloniaApp.Services;

public interface IProtonDbClient
{
    (CompatTier Tier, string? Note) Lookup(string appId, string title);
}

// TODO: replace with real ProtonDB (protondb.com/api) and AreWeAntiCheatYet
// network lookups (Architecture.md §5). Those require network calls,
// rate-limiting, and caching that aren't designed yet. Until then this
// ships a small hardcoded table of well-known titles so the Game
// Compatibility modal is demoable end-to-end with real UI/data plumbing.
public class ProtonDbClient : IProtonDbClient
{
    private static readonly Dictionary<string, (CompatTier Tier, string Note)> KnownTitles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Half-Life 2"] = (CompatTier.RunsPerfectly, "Native Linux support via Steam Play."),
            ["Portal 2"] = (CompatTier.RunsPerfectly, "Native Linux support via Steam Play."),
            ["Counter-Strike 2"] = (CompatTier.RunsPerfectly, "Native Linux support via Steam Play."),
            ["Elden Ring"] = (CompatTier.RunsFine, "Runs well under Proton; EasyAntiCheat is supported."),
            ["Cyberpunk 2077"] = (CompatTier.RunsFine, "Runs well under Proton-GE; occasional shader stutter."),
            ["Grand Theft Auto V"] = (CompatTier.RunsFine, "Runs well under Proton."),
            ["Destiny 2"] = (CompatTier.Unknown, "Anti-cheat compatibility varies; check AreWeAntiCheatYet before installing."),
            ["Valorant"] = (CompatTier.Unknown, "Kernel-level anti-cheat is not supported on Linux."),
        };

    public (CompatTier Tier, string? Note) Lookup(string appId, string title)
    {
        if (KnownTitles.TryGetValue(title, out var entry))
            return (entry.Tier, entry.Note);

        return (CompatTier.Unknown, null);
    }
}
