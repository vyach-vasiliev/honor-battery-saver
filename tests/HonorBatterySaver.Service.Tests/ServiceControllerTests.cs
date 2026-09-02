using HonorBatterySaver.Core;
using HonorBatterySaver.Service;

namespace HonorBatterySaver.Service.Tests;

public sealed class ServiceControllerTests
{
    [Fact]
    public async Task RejectsInvalidEnumWithoutCallingHardware()
    {
        var applier = new FakeApplier();
        var controller = new BatteryServiceController(applier);

        var response = await controller.HandleAsync(
            new IpcRequest(IpcOperation.ApplyMode, (BatteryMode)99), TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal(0, applier.ApplyCount);
    }

    [Fact]
    public async Task RepeatedModeIsSuppressedButForceCallsHardwareAgain()
    {
        var applier = new FakeApplier();
        var controller = new BatteryServiceController(applier);
        var request = new IpcRequest(IpcOperation.ApplyMode, BatteryMode.Home);

        var cancellationToken = TestContext.Current.CancellationToken;
        await controller.HandleAsync(request, cancellationToken);
        await controller.HandleAsync(request, cancellationToken);
        await controller.HandleAsync(request with { Force = true }, cancellationToken);

        Assert.Equal(2, applier.ApplyCount);
    }

    [Fact]
    public async Task UsesUiCultureRequestedByTheTrayClient()
    {
        var controller = new BatteryServiceController(new FakeApplier());
        var cancellationToken = TestContext.Current.CancellationToken;

        var english = await controller.HandleAsync(
            new IpcRequest(IpcOperation.Ping, UiCulture: "en-US"), cancellationToken);
        var russian = await controller.HandleAsync(
            new IpcRequest(IpcOperation.Ping, UiCulture: "ru-RU"), cancellationToken);

        Assert.Equal("Service available.", english.Message);
        Assert.Equal(Strings.Get("Service_Available", System.Globalization.CultureInfo.GetCultureInfo("ru")),
            russian.Message);
    }

    private sealed class FakeApplier : IBatteryProfileApplier
    {
        public int ApplyCount { get; private set; }

        public Task<(bool Supported, bool WmiAvailable, RegistrySnapshot Registry, string Message)> ProbeAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult((true, true, new RegistrySnapshot(true, 1, 0), "OK"));

        public Task<ApplyResult> ApplyAsync(BatteryMode mode, CancellationToken cancellationToken)
        {
            ApplyCount++;
            return Task.FromResult(new ApplyResult(
                ApplyOutcome.Success, mode, DateTimeOffset.Now, true, 0, string.Empty, true, "OK"));
        }
    }
}
