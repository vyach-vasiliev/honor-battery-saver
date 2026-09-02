namespace HonorBatterySaver.Core;

public enum PowerSource
{
    Unknown,
    Battery,
    Ac
}

public sealed record DecisionInput(
    PowerSource PowerSource,
    IReadOnlyList<string> ConnectedSsids,
    AppSettings Settings);

public sealed record DecisionResult(
    BatteryMode? Mode,
    string Reason,
    NetworkRule? MatchedRule = null,
    string? MatchedSsid = null);

public sealed class BatteryModeDecisionEngine
{
    public DecisionResult Decide(DecisionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Settings);

        if (!input.Settings.AutomaticMode)
        {
            return new(input.Settings.ManualMode, Strings.Get("Decision_Manual"));
        }

        if (input.PowerSource != PowerSource.Ac)
        {
            var reason = input.PowerSource == PowerSource.Battery
                ? Strings.Get("Decision_OnBattery")
                : Strings.Get("Decision_UnknownPower");
            return new(null, reason);
        }

        foreach (var rule in input.Settings.NetworkRules.Where(rule => rule.IsEnabled))
        {
            var matchedSsid = input.ConnectedSsids.FirstOrDefault(ssid =>
                string.Equals(ssid, rule.Ssid, StringComparison.Ordinal));
            if (matchedSsid is not null)
            {
                return new(rule.Mode, Strings.Format("Decision_RuleMatched", rule.Ssid), rule, matchedSsid);
            }
        }

        return new(input.Settings.DefaultMode, input.ConnectedSsids.Count == 0
            ? Strings.Get("Decision_NoWifi")
            : Strings.Get("Decision_NoRule"));
    }
}

public sealed class ApplyCommandGate
{
    private BatteryMode? _lastSuccessfulMode;

    public BatteryMode? LastSuccessfulMode => _lastSuccessfulMode;

    public bool ShouldApply(BatteryMode desiredMode, bool force) =>
        force || _lastSuccessfulMode != desiredMode;

    public void RecordSuccess(BatteryMode mode) => _lastSuccessfulMode = mode;

    public void Reset() => _lastSuccessfulMode = null;
}
