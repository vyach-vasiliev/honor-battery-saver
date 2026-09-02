using System.Diagnostics;
using System.IO.Pipes;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Tray;

public sealed class SingleInstanceManager : IDisposable
{
    private readonly string _mutexName = $@"Local\HonorBatterySaver.Tray.{Process.GetCurrentProcess().SessionId}";
    private readonly string _pipeName = $"{PipeProtocol.ActivationPipeName}.{Process.GetCurrentProcess().SessionId}";
    private readonly CancellationTokenSource _cancellation = new();
    private Mutex? _mutex;
    private Task? _listener;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        return createdNew;
    }

    public async Task SignalExistingInstanceAsync()
    {
        try
        {
            await using var client = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(timeout.Token);
            await client.WriteAsync(new byte[] { 1 }, timeout.Token);
        }
        catch
        {
            // The first instance may still be starting or exiting.
        }
    }

    public void StartListening(Action activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        _listener = Task.Run(async () =>
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(_cancellation.Token);
                    var signal = new byte[1];
                    if (await server.ReadAsync(signal, _cancellation.Token) > 0)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(activate);
                    }
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                {
                    break;
                }
            }
        });
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _listener?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _mutex.Dispose();
        }

        _cancellation.Dispose();
    }
}
