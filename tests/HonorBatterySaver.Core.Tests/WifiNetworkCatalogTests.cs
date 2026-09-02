using HonorBatterySaver.Core;

namespace HonorBatterySaver.Core.Tests;

public sealed class WifiNetworkCatalogTests
{
    [Fact]
    public void OrdersConnectedThenAvailableThenKnownNetworks()
    {
        WifiNetworkCandidate[] candidates =
        [
            new("Saved B", false, false, true),
            new("Visible B", true, false, false),
            new("Connected", true, true, true),
            new("Saved A", false, false, true),
            new("Visible A", true, false, true)
        ];

        var result = WifiNetworkCatalog.Order(candidates);

        Assert.Equal(
            ["Connected", "Visible A", "Visible B", "Saved A", "Saved B"],
            result.Select(candidate => candidate.Ssid));
    }

    [Fact]
    public void MergesExactDuplicatesWithoutTrimmingSsid()
    {
        WifiNetworkCandidate[] candidates =
        [
            new("Home", true, false, false),
            new("Home", false, false, true),
            new(" Home ", false, false, true)
        ];

        var result = WifiNetworkCatalog.Order(candidates);

        var home = Assert.Single(result, candidate => candidate.Ssid == "Home");
        Assert.True(home.IsAvailable);
        Assert.True(home.IsKnown);
        Assert.Contains(result, candidate => candidate.Ssid == " Home ");
    }
}
