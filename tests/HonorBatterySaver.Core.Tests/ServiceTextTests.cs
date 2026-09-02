using System.Globalization;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Core.Tests;

public sealed class ServiceTextTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru");

    [Fact]
    public void BuildsStatusFromStructuredFlagsInsteadOfStoredMessageLanguage()
    {
        var status = new ServiceStatus(
            true,
            true,
            true,
            new RegistrySnapshot(true, 1, 0),
            null,
            Strings.Get("Wmi_Available", Russian));

        var text = ServiceText.DescribeStatus(status, English);

        Assert.Equal("Service: available. OEM WMI: available.", text);
    }

    [Fact]
    public void RebuildsSuccessfulAttemptWithTheRequestedLanguageAndModeName()
    {
        var attempt = new ApplyResult(
            ApplyOutcome.Success,
            BatteryMode.Home,
            DateTimeOffset.Now,
            true,
            0,
            string.Empty,
            true,
            Strings.Format(Russian, "Service_ModeApplied", Strings.GetModeName(BatteryMode.Home, Russian)));

        var text = ServiceText.DescribeAttempt(attempt, English);

        Assert.Equal("“Home” mode applied.", text);
    }

    [Fact]
    public void TranslatesKnownFailureReturnedByAServiceUsingAnotherLanguage()
    {
        var english = ServiceText.LocalizeKnownMessage(
            Strings.Get("Service_CommandRejected", Russian),
            English);
        var russian = ServiceText.LocalizeKnownMessage(
            "The OEM WMI provider rejected the command.",
            Russian);

        Assert.Equal("The OEM WMI provider rejected the command.", english);
        Assert.Equal(Strings.Get("Service_CommandRejected", Russian), russian);
    }
}
