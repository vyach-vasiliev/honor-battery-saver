using HonorBatterySaver.Core;
using System.Management;

namespace HonorBatterySaver.Service;

public sealed class HonorWmiBatteryProfileApplier(
    IOemWmiGateway wmi,
    IBatteryRegistry registry,
    RotatingFileLog log) : IBatteryProfileApplier
{
    public async Task<(bool Supported, bool WmiAvailable, RegistrySnapshot Registry, string Message)> ProbeAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var registryState = await registry.ReadAsync(cancellationToken);
            if (!registryState.KeyExists)
            {
                return (false, false, registryState, Strings.Get("Service_RegistryKeyMissing"));
            }

            var wmiProbe = await wmi.ProbeAsync(cancellationToken);
            return (wmiProbe.IsAvailable, wmiProbe.IsAvailable, registryState, wmiProbe.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log.Write("ERROR", $"Hardware probe failed: {exception.GetType().Name}.");
            return (false, false, new RegistrySnapshot(false, null, null),
                Strings.Format("Service_ProbeFailed", exception.GetType().Name));
        }
    }

    public async Task<ApplyResult> ApplyAsync(BatteryMode mode, CancellationToken cancellationToken)
    {
        var attemptedAt = DateTimeOffset.Now;
        if (!BatteryProfiles.IsSupported(mode))
        {
            return new(ApplyOutcome.Failed, mode, attemptedAt, null, null, null, false,
                Strings.Get("Ipc_InvalidBatteryMode"));
        }

        try
        {
            var registryState = await registry.ReadAsync(cancellationToken);
            if (!registryState.KeyExists)
            {
                return new(ApplyOutcome.Unsupported, mode, attemptedAt, null, null, null, false,
                    Strings.Get("Service_DeviceUnsupported"));
            }

            var probe = await wmi.ProbeAsync(cancellationToken);
            if (!probe.IsAvailable)
            {
                return new(ApplyOutcome.Unsupported, mode, attemptedAt, null, null, null, false, probe.Message);
            }

            var profile = BatteryProfiles.Get(mode);
            var invocation = await wmi.InvokeAsync(profile.OemPayload, cancellationToken);
            var outputHex = Convert.ToHexString(invocation.Output);
            log.Write("INFO", $"OEM result={invocation.ReturnValue}; reserved={invocation.Reserved}; output={outputHex}.");
            if (!invocation.ReturnValue)
            {
                return new(ApplyOutcome.Failed, mode, attemptedAt, false, invocation.Reserved, outputHex, false,
                    Strings.Get("Service_CommandRejected"));
            }

            try
            {
                await registry.WriteAsync(mode, cancellationToken);
                return new(ApplyOutcome.Success, mode, attemptedAt, true, invocation.Reserved, outputHex, true,
                    Strings.Format("Service_ModeApplied", profile.DisplayName));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                log.Write("ERROR", $"Registry synchronization failed: {exception.GetType().Name}.");
                return new(ApplyOutcome.PartialFailure, mode, attemptedAt, true, invocation.Reserved, outputHex, false,
                    Strings.Get("Service_RegistrySyncFailed"));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log.Write("ERROR", $"Profile apply failed: {DescribeException(exception)}");
            return new(ApplyOutcome.Failed, mode, attemptedAt, null, null, null, false,
                ToSafeApplyMessage(exception));
        }
    }

    private static string DescribeException(Exception exception) => exception switch
    {
        ManagementException managementException =>
            $"ManagementException; status={managementException.ErrorCode}; message={managementException.Message}",
        _ => $"{exception.GetType().Name}; message={exception.Message}"
    };

    private static string ToSafeApplyMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => Strings.Get("Wmi_AccessDenied"),
        ManagementException managementException when managementException.ErrorCode is
            ManagementStatus.AccessDenied or ManagementStatus.PrivilegeNotHeld =>
            Strings.Get("Wmi_ServiceNeedsElevation"),
        ManagementException managementException when managementException.ErrorCode is
            ManagementStatus.InvalidMethod or ManagementStatus.MethodNotImplemented or ManagementStatus.MethodDisabled =>
            Strings.Get("Wmi_MethodUnavailable"),
        ManagementException managementException when managementException.ErrorCode is
            ManagementStatus.InvalidMethodParameters or ManagementStatus.InvalidParameter or ManagementStatus.TypeMismatch =>
            Strings.Get("Wmi_CommandFormatRejected"),
        ManagementException managementException =>
            Strings.Format("Wmi_CallFailed", managementException.ErrorCode),
        _ => Strings.Format("Service_ApplyFailed", exception.GetType().Name)
    };
}
