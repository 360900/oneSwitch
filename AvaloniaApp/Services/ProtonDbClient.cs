using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApp.Models;

namespace AvaloniaApp.Services;

public record ProtonDbLookupResult(CompatTier Tier, string? Note, bool Succeeded);

public interface IProtonDbClient
{
    // Returns one result per input app, in the same order. Never throws: per-app
    // network/parse failures degrade to (Unknown, null, Succeeded: false).
    Task<IReadOnlyList<ProtonDbLookupResult>> LookupAsync(
        IReadOnlyList<InstalledSteamApp> apps,
        CancellationToken cancellationToken = default);
}

// Real ProtonDB integration (Architecture.md §5) against the community summaries API
// (protondb.com/api/v1/reports/summaries/{appId}.json). Results are cached in memory
// for the app session and requests are throttled so a large library doesn't hammer
// the API. AreWeAntiCheatYet is still not integrated - anti-cheat-specific caveats
// only surface here insofar as ProtonDB's own tier already reflects them.
public class ProtonDbClient : IProtonDbClient
{
    private const string SummaryEndpoint = "https://www.protondb.com/api/v1/reports/summaries/";

    // ProtonDB's API is a free community resource; keep concurrency low.
    private const int MaxConcurrentRequests = 4;

    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, ProtonDbLookupResult> Cache = new();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        // ProtonDB asks API consumers to send an identifying User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kargo/0.1 (windows-to-linux migration tool)");
        return client;
    }

    public async Task<IReadOnlyList<ProtonDbLookupResult>> LookupAsync(
        IReadOnlyList<InstalledSteamApp> apps,
        CancellationToken cancellationToken = default)
    {
        using var throttle = new SemaphoreSlim(MaxConcurrentRequests);
        var tasks = apps.Select(async app =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await LookupOneAsync(app.AppId, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                throttle.Release();
            }
        });

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<ProtonDbLookupResult> LookupOneAsync(string appId, CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(appId, out var cached))
            return cached;

        ProtonDbLookupResult result;
        try
        {
            using var response = await Http.GetAsync(SummaryEndpoint + appId + ".json", cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // No reports at all for this appId - a legitimate "we don't know".
                result = new ProtonDbLookupResult(CompatTier.Unknown, null, Succeeded: true);
            }
            else
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var summary = JsonSerializer.Deserialize<SummaryDto>(json);
                result = MapSummary(summary);
            }

            // Only cache authoritative answers; transient failures stay retryable.
            Cache[appId] = result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            result = new ProtonDbLookupResult(CompatTier.Unknown, null, Succeeded: false);
        }

        return result;
    }

    private static ProtonDbLookupResult MapSummary(SummaryDto? summary)
    {
        if (summary?.Tier is null)
            return new ProtonDbLookupResult(CompatTier.Unknown, null, Succeeded: true);

        var tier = summary.Tier.Trim().ToLowerInvariant();
        var compatTier = tier switch
        {
            "platinum" => CompatTier.RunsPerfectly,
            "gold" or "silver" => CompatTier.RunsFine,
            "bronze" or "borked" => CompatTier.WontRun,
            // "pending" and anything unrecognized: not enough data for a verdict.
            _ => CompatTier.Unknown,
        };

        string? note = compatTier == CompatTier.Unknown
            ? "Not enough ProtonDB reports for a verdict."
            : $"{Capitalize(tier)} on ProtonDB ({summary.Total?.ToString("N0") ?? "?"} reports, {summary.Confidence ?? "unknown"} confidence)";

        return new ProtonDbLookupResult(compatTier, note, Succeeded: true);
    }

    private static string Capitalize(string value) =>
        char.ToUpperInvariant(value[0]) + value[1..];

    private sealed class SummaryDto
    {
        [JsonPropertyName("tier")]
        public string? Tier { get; set; }

        [JsonPropertyName("confidence")]
        public string? Confidence { get; set; }

        [JsonPropertyName("total")]
        public int? Total { get; set; }
    }
}
