using System.Globalization;

namespace HonorBatterySaver.Core;

public static class ServiceText
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru");

    private static readonly string[] KnownMessageKeys =
    [
        "Service_Available",
        "Service_ModeAlreadyApplied",
        "Service_RegistryKeyMissing",
        "Service_DeviceUnsupported",
        "Service_CommandRejected",
        "Service_RegistrySyncFailed",
        "Wmi_MethodMissing",
        "Wmi_InstanceMissing",
        "Wmi_ActiveInstanceMissing",
        "Wmi_HwmiInstanceMissing",
        "Wmi_Available",
        "Wmi_AccessDenied",
        "Wmi_RunServiceAsAdmin",
        "Wmi_ClassMissing",
        "Wmi_NamespaceUnavailable",
        "Wmi_ServiceNeedsElevation",
        "Wmi_MethodUnavailable",
        "Wmi_CommandFormatRejected"
    ];

    private static readonly string[] KnownFormattedMessageKeys =
    [
        "Service_ProbeFailed",
        "Service_ApplyFailed",
        "Wmi_UnavailableError",
        "Wmi_CallFailed"
    ];

    public static string DescribeStatus(ServiceStatus status, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(culture);

        if (status.WmiAvailable)
        {
            return Strings.Get("Diagnostics_ServiceReady", culture);
        }

        return Strings.Format(
            culture,
            "Diagnostics_ServiceProblem",
            LocalizeKnownMessage(status.Message, culture));
    }

    public static string DescribeAttempt(ApplyResult attempt, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(culture);

        return attempt.Outcome switch
        {
            ApplyOutcome.Success => Strings.Format(
                culture,
                "Service_ModeApplied",
                Strings.GetModeName(attempt.Mode, culture)),
            ApplyOutcome.PartialFailure => Strings.Get("Service_RegistrySyncFailed", culture),
            _ => LocalizeKnownMessage(attempt.Message, culture)
        };
    }

    public static string LocalizeKnownMessage(string message, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        foreach (var key in KnownMessageKeys)
        {
            if (Matches(message, key, English) || Matches(message, key, Russian))
            {
                return Strings.Get(key, culture);
            }
        }

        foreach (var key in KnownFormattedMessageKeys)
        {
            if (TryExtractArgument(Strings.Get(key, English), message, out var argument) ||
                TryExtractArgument(Strings.Get(key, Russian), message, out argument))
            {
                return Strings.Format(culture, key, argument);
            }
        }

        return message;
    }

    private static bool Matches(string message, string key, CultureInfo culture) =>
        string.Equals(message, Strings.Get(key, culture), StringComparison.Ordinal);

    private static bool TryExtractArgument(string template, string message, out string argument)
    {
        const string placeholder = "{0}";
        var placeholderIndex = template.IndexOf(placeholder, StringComparison.Ordinal);
        if (placeholderIndex < 0)
        {
            argument = string.Empty;
            return false;
        }

        var prefix = template[..placeholderIndex];
        var suffix = template[(placeholderIndex + placeholder.Length)..];
        if (!message.StartsWith(prefix, StringComparison.Ordinal) ||
            !message.EndsWith(suffix, StringComparison.Ordinal) ||
            message.Length < prefix.Length + suffix.Length)
        {
            argument = string.Empty;
            return false;
        }

        argument = message.Substring(prefix.Length, message.Length - prefix.Length - suffix.Length);
        return true;
    }
}
