using HonorBatterySaver.Core;

namespace HonorBatterySaver.Core.Tests;

public sealed class DecisionEngineTests
{
    private readonly BatteryModeDecisionEngine _engine = new();

    [Fact]
    public void DefaultsToTravelOnAcWithoutRules()
    {
        var result = Decide(PowerSource.Ac, ["Cafe"], AppSettings.CreateDefault());
        Assert.Equal(BatteryMode.Travel, result.Mode);
    }

    [Fact]
    public void WifiOffOnAcUsesTravel()
    {
        var settings = AppSettings.CreateDefault();
        settings.NetworkRules.Add(new NetworkRule { Ssid = "HomeWiFi", Mode = BatteryMode.Home });

        Assert.Equal(BatteryMode.Travel, Decide(PowerSource.Ac, [], settings).Mode);
    }

    [Theory]
    [InlineData(PowerSource.Battery, "HomeWiFi", null)]
    [InlineData(PowerSource.Battery, "Unknown", null)]
    [InlineData(PowerSource.Unknown, "HomeWiFi", null)]
    [InlineData(PowerSource.Ac, "HomeWiFi", BatteryMode.Home)]
    [InlineData(PowerSource.Ac, "Unknown", BatteryMode.Travel)]
    public void ImplementsAutomaticDecisionMatrix(PowerSource source, string ssid, BatteryMode? expected)
    {
        var settings = AppSettings.CreateDefault();
        settings.NetworkRules.Add(new NetworkRule { Ssid = "HomeWiFi", Mode = BatteryMode.Home });

        Assert.Equal(expected, Decide(source, [ssid], settings).Mode);
    }

    [Fact]
    public void AutomaticModeDoesNotSelectHardwareProfileOnBattery()
    {
        var result = Decide(PowerSource.Battery, ["HomeWiFi"], AppSettings.CreateDefault());

        Assert.Null(result.Mode);
        Assert.Equal(Strings.Get("Decision_OnBattery"), result.Reason);
    }

    [Fact]
    public void UsesFirstMatchingRuleInSettingsOrderAcrossInterfaces()
    {
        var settings = AppSettings.CreateDefault();
        settings.NetworkRules.Add(new NetworkRule { Ssid = "SecondInterface", Mode = BatteryMode.Office });
        settings.NetworkRules.Add(new NetworkRule { Ssid = "FirstInterface", Mode = BatteryMode.Home });

        var result = Decide(PowerSource.Ac, ["FirstInterface", "SecondInterface"], settings);

        Assert.Equal(BatteryMode.Office, result.Mode);
        Assert.Equal("SecondInterface", result.MatchedSsid);
    }

    [Fact]
    public void MatchesSsidOrdinallyWithoutTrimming()
    {
        var settings = AppSettings.CreateDefault();
        settings.NetworkRules.Add(new NetworkRule { Ssid = " Home ", Mode = BatteryMode.Home });

        Assert.Equal(BatteryMode.Travel, Decide(PowerSource.Ac, ["Home"], settings).Mode);
        Assert.Equal(BatteryMode.Home, Decide(PowerSource.Ac, [" Home "], settings).Mode);
    }

    [Fact]
    public void ManualSelectionDisablesAutomationAndPersistsMode()
    {
        var settings = AppSettings.CreateDefault();

        SettingsBehavior.SelectManualMode(settings, BatteryMode.Office);

        Assert.False(settings.AutomaticMode);
        Assert.Equal(BatteryMode.Office, Decide(PowerSource.Battery, [], settings).Mode);
    }

    private DecisionResult Decide(PowerSource source, IReadOnlyList<string> ssids, AppSettings settings) =>
        _engine.Decide(new(source, ssids, settings));
}
