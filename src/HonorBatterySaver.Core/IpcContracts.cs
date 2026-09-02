using System.Buffers.Binary;
using System.Text.Json;

namespace HonorBatterySaver.Core;

public enum IpcOperation
{
    Ping,
    GetStatus,
    ApplyMode
}

public sealed record IpcRequest(
    IpcOperation Operation,
    BatteryMode? Mode = null,
    bool Force = false,
    string? UiCulture = null);

public enum ApplyOutcome
{
    Success,
    PartialFailure,
    Failed,
    Unsupported
}

public sealed record RegistrySnapshot(
    bool KeyExists,
    int? Status,
    int? Mode,
    string? StatusKind = null,
    string? ModeKind = null);

public sealed record ApplyResult(
    ApplyOutcome Outcome,
    BatteryMode Mode,
    DateTimeOffset AttemptedAt,
    bool? WmiResult,
    uint? WmiReserved,
    string? WmiOutputHex,
    bool RegistrySynchronized,
    string Message);

public sealed record ServiceStatus(
    bool ServiceAvailable,
    bool DeviceSupported,
    bool WmiAvailable,
    RegistrySnapshot Registry,
    ApplyResult? LastAttempt,
    string Message);

public sealed record IpcResponse(bool Success, string Message, ApplyResult? ApplyResult = null, ServiceStatus? Status = null);

public static class IpcRequestValidator
{
    public static string? Validate(IpcRequest? request)
    {
        if (request is null)
        {
            return Strings.Get("Ipc_EmptyRequest");
        }

        if (!Enum.IsDefined(request.Operation))
        {
            return Strings.Get("Ipc_UnknownOperation");
        }

        if (request.Operation == IpcOperation.ApplyMode)
        {
            if (request.Mode is null || !BatteryProfiles.IsSupported(request.Mode.Value))
            {
                return Strings.Get("Ipc_InvalidBatteryMode");
            }
        }
        else if (request.Mode is not null || request.Force)
        {
            return Strings.Get("Ipc_ModeParametersOnlyApply");
        }

        return null;
    }
}

public static class PipeProtocol
{
    public const string WindowsServiceName = "Honor Battery Saver";
    public const string ServicePipeName = "HonorBatterySaver.Service.v1";
    public const string ActivationPipeName = "HonorBatterySaver.Tray.Activation.v1";
    public const int MaximumMessageBytes = 64 * 1024;

    public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonDefaults.Options);
        if (payload.Length > MaximumMessageBytes)
        {
            throw new InvalidDataException("IPC message is too large.");
        }

        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > MaximumMessageBytes)
        {
            throw new InvalidDataException("Invalid IPC message length.");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize<T>(payload, JsonDefaults.Options)
            ?? throw new InvalidDataException("IPC message is empty.");
    }
}
