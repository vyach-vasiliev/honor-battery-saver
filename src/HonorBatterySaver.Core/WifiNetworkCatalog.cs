namespace HonorBatterySaver.Core;

public sealed record WifiNetworkCandidate(
    string Ssid,
    bool IsAvailable,
    bool IsConnected,
    bool IsKnown);

public static class WifiNetworkCatalog
{
    public static IReadOnlyList<WifiNetworkCandidate> Order(IEnumerable<WifiNetworkCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(candidate => !string.IsNullOrEmpty(candidate.Ssid))
            .GroupBy(candidate => candidate.Ssid, StringComparer.Ordinal)
            .Select(group => new WifiNetworkCandidate(
                group.Key,
                group.Any(candidate => candidate.IsAvailable),
                group.Any(candidate => candidate.IsConnected),
                group.Any(candidate => candidate.IsKnown)))
            .OrderByDescending(candidate => candidate.IsConnected)
            .ThenByDescending(candidate => candidate.IsAvailable)
            .ThenBy(candidate => candidate.Ssid, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Ssid, StringComparer.Ordinal)
            .ToArray();
    }
}
