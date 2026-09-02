using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using HonorBatterySaver.Core;
using Microsoft.Extensions.Hosting;

namespace HonorBatterySaver.Service;

public sealed class NamedPipeWorker(BatteryServiceController controller, RotatingFileLog log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.Write("INFO", "Service pipe worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreatePipe();
                await pipe.WaitForConnectionAsync(stoppingToken);
                await ProcessConnectionAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                log.Write("ERROR", $"Pipe request failed: {exception.GetType().Name}.");
            }
        }

        log.Write("INFO", "Service pipe worker stopped.");
    }

    private async Task ProcessConnectionAsync(Stream pipe, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        IpcResponse response;
        try
        {
            var request = await PipeProtocol.ReadAsync<IpcRequest>(pipe, timeout.Token);
            response = await controller.HandleAsync(request, timeout.Token);
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException)
        {
            response = new(false, Strings.Get("Ipc_InvalidRequest"));
        }

        await PipeProtocol.WriteAsync(pipe, response, timeout.Token);
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeProtocol.ServicePipeName,
            PipeDirection.InOut,
            4,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough,
            4096,
            4096,
            security);
    }
}
