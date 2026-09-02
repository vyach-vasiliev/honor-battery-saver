using System.Windows;
using HonorBatterySaver.Core;
using MessageBox = System.Windows.MessageBox;

namespace HonorBatterySaver.Tray;

public partial class App : System.Windows.Application
{
    private SingleInstanceManager? _singleInstance;
    private TrayApplicationController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Strings.ApplySupportedUiCulture();
        ThemeManager.Initialize();
        if (ServiceRecoveryManager.IsRecoveryChild(e.Args))
        {
            Shutdown(await ServiceRecoveryManager.RunRecoveryChildAsync());
            return;
        }

        if (e.Args.Length >= 2 && string.Equals(e.Args[0], "--hardware-diagnostic", StringComparison.OrdinalIgnoreCase))
        {
            await HardwareDiagnosticRunner.RunElevatedChildAsync(
                e.Args[1],
                e.Args.Length >= 3 ? e.Args[2] : UiLanguage.System.ToString());
            Shutdown();
            return;
        }

        _singleInstance = new SingleInstanceManager();
        if (!_singleInstance.TryAcquire())
        {
            await _singleInstance.SignalExistingInstanceAsync();
            Shutdown();
            return;
        }

        _controller = new TrayApplicationController(Dispatcher);
        _singleInstance.StartListening(_controller.ShowSettings);
        try
        {
            await _controller.InitializeAsync();
            ServiceRecoveryResult serviceRecovery;
            try
            {
                serviceRecovery = await ServiceRecoveryManager.EnsureRunningAsync();
            }
            catch (Exception exception)
            {
                serviceRecovery = new(ServiceRecoveryOutcome.Failed,
                    Strings.Format("App_ServiceStartCheckFailed", exception.Message));
            }

            if (!serviceRecovery.Success)
            {
                _controller.NotifyServiceRecoveryFailure(serviceRecovery.Message);
            }

            _controller.Start();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                Strings.Format("App_StartFailed", exception.Message),
                Strings.Get("App_Name"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
