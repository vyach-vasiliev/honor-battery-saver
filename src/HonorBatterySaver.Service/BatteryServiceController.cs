using HonorBatterySaver.Core;

namespace HonorBatterySaver.Service;

public sealed class BatteryServiceController(IBatteryProfileApplier applier)
{
    private readonly SemaphoreSlim _applyLock = new(1, 1);
    private ApplyResult? _lastAttempt;

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        using var cultureScope = Strings.UseCulture(request.UiCulture);
        var validationError = IpcRequestValidator.Validate(request);
        if (validationError is not null)
        {
            return new(false, validationError);
        }

        return request.Operation switch
        {
            IpcOperation.Ping => new(true, Strings.Get("Service_Available")),
            IpcOperation.GetStatus => await GetStatusAsync(cancellationToken),
            IpcOperation.ApplyMode => await ApplyAsync(request.Mode!.Value, request.Force, cancellationToken),
            _ => new(false, Strings.Get("Ipc_UnknownOperation"))
        };
    }

    private async Task<IpcResponse> ApplyAsync(BatteryMode mode, bool force, CancellationToken cancellationToken)
    {
        await _applyLock.WaitAsync(cancellationToken);
        try
        {
            if (!force && _lastAttempt is { Outcome: ApplyOutcome.Success } last && last.Mode == mode)
            {
                return new(true, Strings.Get("Service_ModeAlreadyApplied"), last);
            }

            _lastAttempt = await applier.ApplyAsync(mode, cancellationToken);
            return new(_lastAttempt.Outcome == ApplyOutcome.Success, _lastAttempt.Message, _lastAttempt);
        }
        finally
        {
            _applyLock.Release();
        }
    }

    private async Task<IpcResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var probe = await applier.ProbeAsync(cancellationToken);
        var status = new ServiceStatus(
            true,
            probe.Supported,
            probe.WmiAvailable,
            probe.Registry,
            _lastAttempt,
            probe.Message);
        return new(true, probe.Message, Status: status);
    }
}
