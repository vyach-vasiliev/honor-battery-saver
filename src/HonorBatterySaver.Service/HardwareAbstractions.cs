using HonorBatterySaver.Core;

namespace HonorBatterySaver.Service;

public sealed record WmiProbe(bool IsAvailable, string Message);
public sealed record WmiInvocation(bool ReturnValue, uint Reserved, byte[] Output);

public interface IOemWmiGateway
{
    Task<WmiProbe> ProbeAsync(CancellationToken cancellationToken);
    Task<WmiInvocation> InvokeAsync(byte[] payload, CancellationToken cancellationToken);
}

public interface IBatteryRegistry
{
    Task<RegistrySnapshot> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(BatteryMode mode, CancellationToken cancellationToken);
}

public interface IBatteryProfileApplier
{
    Task<(bool Supported, bool WmiAvailable, RegistrySnapshot Registry, string Message)> ProbeAsync(
        CancellationToken cancellationToken);

    Task<ApplyResult> ApplyAsync(BatteryMode mode, CancellationToken cancellationToken);
}
