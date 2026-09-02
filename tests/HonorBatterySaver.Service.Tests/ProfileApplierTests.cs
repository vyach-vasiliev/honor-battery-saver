using HonorBatterySaver.Core;
using HonorBatterySaver.Service;

namespace HonorBatterySaver.Service.Tests;

public sealed class ProfileApplierTests : IDisposable
{
    private readonly string _logPath = Path.Combine(Path.GetTempPath(), "HonorBatterySaver.Tests", Guid.NewGuid().ToString("N"), "service.log");

    [Theory]
    [InlineData(BatteryMode.Home, "03102846")]
    [InlineData(BatteryMode.Office, "0310465A")]
    [InlineData(BatteryMode.Travel, "03105F64")]
    public async Task SendsOnlyCatalogPayloadThenWritesRegistry(BatteryMode mode, string expectedPayload)
    {
        var wmi = new FakeWmi { Invocation = new(true, 7, [0xAA]) };
        var registry = new FakeRegistry();
        var applier = CreateApplier(wmi, registry);

        var result = await applier.ApplyAsync(mode, TestContext.Current.CancellationToken);

        Assert.Equal(ApplyOutcome.Success, result.Outcome);
        Assert.Equal(expectedPayload, Convert.ToHexString(Assert.Single(wmi.Payloads)));
        Assert.Equal(mode, Assert.Single(registry.Writes));
        Assert.Equal("AA", result.WmiOutputHex);
    }

    [Fact]
    public async Task DoesNotWriteRegistryWhenWmiReturnsFalse()
    {
        var registry = new FakeRegistry();
        var applier = CreateApplier(new FakeWmi { Invocation = new(false, 0, []) }, registry);

        var result = await applier.ApplyAsync(BatteryMode.Home, TestContext.Current.CancellationToken);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Empty(registry.Writes);
    }

    [Fact]
    public async Task ReportsPartialFailureWhenRegistryWriteFailsAfterWmiSuccess()
    {
        var registry = new FakeRegistry { ThrowOnWrite = true };
        var applier = CreateApplier(new FakeWmi { Invocation = new(true, 0, []) }, registry);

        var result = await applier.ApplyAsync(BatteryMode.Office, TestContext.Current.CancellationToken);

        Assert.Equal(ApplyOutcome.PartialFailure, result.Outcome);
        Assert.True(result.WmiResult);
        Assert.False(result.RegistrySynchronized);
    }

    [Fact]
    public async Task MissingOemRegistryPreventsAnyWmiCall()
    {
        var wmi = new FakeWmi();
        var registry = new FakeRegistry { Snapshot = new(false, null, null) };

        var result = await CreateApplier(wmi, registry).ApplyAsync(
            BatteryMode.Travel, TestContext.Current.CancellationToken);

        Assert.Equal(ApplyOutcome.Unsupported, result.Outcome);
        Assert.Equal(0, wmi.ProbeCount);
        Assert.Empty(wmi.Payloads);
    }

    private HonorWmiBatteryProfileApplier CreateApplier(FakeWmi wmi, FakeRegistry registry) =>
        new(wmi, registry, new RotatingFileLog(_logPath));

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_logPath)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FakeWmi : IOemWmiGateway
    {
        public WmiProbe Probe { get; set; } = new(true, "OK");
        public WmiInvocation Invocation { get; set; } = new(true, 0, []);
        public int ProbeCount { get; private set; }
        public List<byte[]> Payloads { get; } = [];

        public Task<WmiProbe> ProbeAsync(CancellationToken cancellationToken)
        {
            ProbeCount++;
            return Task.FromResult(Probe);
        }

        public Task<WmiInvocation> InvokeAsync(byte[] payload, CancellationToken cancellationToken)
        {
            Payloads.Add([.. payload]);
            return Task.FromResult(Invocation);
        }
    }

    private sealed class FakeRegistry : IBatteryRegistry
    {
        public RegistrySnapshot Snapshot { get; set; } = new(true, 1, 2);
        public bool ThrowOnWrite { get; set; }
        public List<BatteryMode> Writes { get; } = [];

        public Task<RegistrySnapshot> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Snapshot);

        public Task WriteAsync(BatteryMode mode, CancellationToken cancellationToken)
        {
            if (ThrowOnWrite)
            {
                throw new IOException("Test failure.");
            }

            Writes.Add(mode);
            return Task.CompletedTask;
        }
    }
}
