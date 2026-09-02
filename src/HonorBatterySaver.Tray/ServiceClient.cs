using System.IO;
using System.IO.Pipes;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Tray;

public sealed class ServiceClient
{
    public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken = default)
        => await SendAsync(request, 3000, TimeSpan.FromSeconds(10), cancellationToken);

    internal async Task<IpcResponse> SendAsync(
        IpcRequest request,
        int connectTimeoutMilliseconds,
        TimeSpan operationTimeout,
        CancellationToken cancellationToken = default)
    {
        request = request with { UiCulture = Strings.CurrentCulture.Name };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(operationTimeout);
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".", PipeProtocol.ServicePipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(connectTimeoutMilliseconds, timeout.Token);
            await PipeProtocol.WriteAsync(pipe, request, timeout.Token);
            return await PipeProtocol.ReadAsync<IpcResponse>(pipe, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, Strings.Get("ServiceClient_Timeout"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, Strings.Get("ServiceClient_Unavailable"));
        }
        catch (TimeoutException)
        {
            return new(false, Strings.Get("ServiceClient_Unavailable"));
        }
    }
}
