using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Tray;

internal enum ServiceRecoveryOutcome
{
    AlreadyRunning,
    Started,
    NotFound,
    Cancelled,
    Failed
}

internal sealed record ServiceRecoveryResult(ServiceRecoveryOutcome Outcome, string Message)
{
    public bool Success => Outcome is ServiceRecoveryOutcome.AlreadyRunning or ServiceRecoveryOutcome.Started;
}

internal static class ServiceRecoveryManager
{
    private const string RecoveryArgument = "--recover-service";
    private const string ServiceExecutableName = "HonorBatterySaver.Service.exe";
    private const int ServiceDoesNotExist = 1060;
    private const int ElevationCancelled = 1223;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(900);

    public static bool IsRecoveryChild(IReadOnlyList<string> arguments) =>
        arguments.Count == 1 && string.Equals(arguments[0], RecoveryArgument, StringComparison.OrdinalIgnoreCase);

    public static async Task<ServiceRecoveryResult> EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        if (await ProbeAsync(cancellationToken))
        {
            return new(ServiceRecoveryOutcome.AlreadyRunning, Strings.Get("Recovery_AlreadyRunning"));
        }

        var existingProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ServiceExecutableName));
        var serviceProcessExists = existingProcesses.Length > 0;
        foreach (var process in existingProcesses)
        {
            process.Dispose();
        }

        if (serviceProcessExists)
        {
            if (await WaitForPipeAsync(cancellationToken))
            {
                return new(ServiceRecoveryOutcome.Started, Strings.Get("Recovery_FinishedStarting"));
            }

            return new(ServiceRecoveryOutcome.Failed, Strings.Get("Recovery_ProcessPipeUnavailable"));
        }

        var query = await RunScAsync(["query", PipeProtocol.WindowsServiceName], cancellationToken);
        if (query.ExitCode != ServiceDoesNotExist)
        {
            return await StartRegisteredServiceElevatedAsync(cancellationToken);
        }

        var serviceExecutable = FindServiceExecutable();
        if (serviceExecutable is null)
        {
            return new(ServiceRecoveryOutcome.NotFound, Strings.Get("Recovery_ExecutableNotFoundNearby"));
        }

        return await StartStandaloneServiceElevatedAsync(serviceExecutable, cancellationToken);
    }

    public static async Task<int> RunRecoveryChildAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAdministrator())
        {
            return 5;
        }

        var result = await RecoverFromElevatedProcessAsync(cancellationToken);
        return result.Success ? 0 : 1;
    }

    public static async Task<ServiceRecoveryResult> RecoverFromElevatedProcessAsync(
        CancellationToken cancellationToken = default)
    {
        if (await ProbeAsync(cancellationToken))
        {
            return new(ServiceRecoveryOutcome.AlreadyRunning, Strings.Get("Recovery_AlreadyRunning"));
        }

        var query = await RunScAsync(["query", PipeProtocol.WindowsServiceName], cancellationToken);
        if (query.ExitCode != ServiceDoesNotExist)
        {
            var configure = await RunScAsync(
                ["config", PipeProtocol.WindowsServiceName, "start=", "delayed-auto"], cancellationToken);
            if (configure.ExitCode != 0)
            {
                return new(ServiceRecoveryOutcome.Failed,
                    Strings.Format("Recovery_EnableAutostartFailed", configure.Message));
            }

            var start = await RunScAsync(["start", PipeProtocol.WindowsServiceName], cancellationToken);
            if (start.ExitCode != 0 && !await ProbeAsync(cancellationToken))
            {
                return new(ServiceRecoveryOutcome.Failed,
                    Strings.Format("Recovery_StartFailed", start.Message));
            }

            return await WaitForPipeAsync(cancellationToken)
                ? new(ServiceRecoveryOutcome.Started, Strings.Get("Recovery_WindowsServiceStarted"))
                : new(ServiceRecoveryOutcome.Failed, Strings.Get("Recovery_PipeDidNotAppear"));
        }

        var serviceExecutable = FindServiceExecutable();
        if (serviceExecutable is null)
        {
            return new(ServiceRecoveryOutcome.NotFound, Strings.Get("Recovery_ExecutableNotFound"));
        }

        StartStandaloneService(serviceExecutable, elevated: false);
        return await WaitForPipeAsync(cancellationToken)
            ? new(ServiceRecoveryOutcome.Started, Strings.Get("Recovery_ProcessStartedElevated"))
            : new(ServiceRecoveryOutcome.Failed, Strings.Get("Recovery_ProcessPipeDidNotAppear"));
    }

    private static async Task<ServiceRecoveryResult> StartRegisteredServiceElevatedAsync(
        CancellationToken cancellationToken)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return new(ServiceRecoveryOutcome.Failed, Strings.Get("Recovery_AppPathUnknown"));
        }

        try
        {
            var startInfo = CreateElevatedStartInfo(executable);
            startInfo.ArgumentList.Add(RecoveryArgument);
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(Strings.Get("Recovery_ProcessNotCreated"));
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                return new(ServiceRecoveryOutcome.Failed, Strings.Get("Recovery_WindowsStartFailed"));
            }

            return await WaitForPipeAsync(cancellationToken)
                ? new(ServiceRecoveryOutcome.Started, Strings.Get("Recovery_WindowsServiceStarted"))
                : new(ServiceRecoveryOutcome.Failed, Strings.Get("Recovery_PipeDidNotAppear"));
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ElevationCancelled)
        {
            return new(ServiceRecoveryOutcome.Cancelled, Strings.Get("Hardware_ElevationCancelled"));
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new(ServiceRecoveryOutcome.Failed,
                Strings.Format("Recovery_ElevationFailed", exception.Message));
        }
    }

    private static async Task<ServiceRecoveryResult> StartStandaloneServiceElevatedAsync(
        string serviceExecutable,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = StartStandaloneService(serviceExecutable, elevated: true);
            return await WaitForPipeAsync(cancellationToken)
                ? new(ServiceRecoveryOutcome.Started, Strings.Get("Recovery_ProcessStartedElevated"))
                : new(ServiceRecoveryOutcome.Failed, Strings.Get("Recovery_ProcessPipeDidNotAppear"));
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ElevationCancelled)
        {
            return new(ServiceRecoveryOutcome.Cancelled, Strings.Get("Hardware_ElevationCancelled"));
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new(ServiceRecoveryOutcome.Failed,
                Strings.Format("Recovery_ProcessStartFailed", exception.Message));
        }
    }

    private static Process StartStandaloneService(string serviceExecutable, bool elevated)
    {
        var startInfo = elevated
            ? CreateElevatedStartInfo(serviceExecutable)
            : new ProcessStartInfo
            {
                FileName = serviceExecutable,
                WorkingDirectory = Path.GetDirectoryName(serviceExecutable)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException(Strings.Get("Recovery_ServiceProcessNotCreated"));
    }

    private static ProcessStartInfo CreateElevatedStartInfo(string executable) => new()
    {
        FileName = executable,
        WorkingDirectory = Path.GetDirectoryName(executable)!,
        UseShellExecute = true,
        Verb = "runas",
        WindowStyle = ProcessWindowStyle.Hidden
    };

    private static async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        var response = await new ServiceClient().SendAsync(
            new IpcRequest(IpcOperation.Ping),
            connectTimeoutMilliseconds: 600,
            operationTimeout: ProbeTimeout,
            cancellationToken);
        return response.Success;
    }

    private static async Task<bool> WaitForPipeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (await ProbeAsync(cancellationToken))
            {
                return true;
            }

            await Task.Delay(350, cancellationToken);
        }

        return false;
    }

    private static string? FindServiceExecutable()
    {
        var applicationDirectory = Path.GetDirectoryName(typeof(ServiceRecoveryManager).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(applicationDirectory, ServiceExecutableName),
            Path.Combine(applicationDirectory, "Service", ServiceExecutableName)
        };

        var directory = new DirectoryInfo(applicationDirectory);
        for (var depth = 0; depth < 9 && directory is not null; depth++, directory = directory.Parent)
        {
            foreach (var configuration in new[] { "Release", "Debug" })
            {
                var outputRoot = Path.Combine(
                    directory.FullName,
                    "src",
                    "HonorBatterySaver.Service",
                    "bin",
                    configuration,
                    "net10.0-windows10.0.22000.0",
                    "win-x64");
                candidates.Add(Path.Combine(outputRoot, "publish", ServiceExecutableName));
                candidates.Add(Path.Combine(outputRoot, ServiceExecutableName));
            }
        }

        return candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task<ScResult> RunScAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(Strings.Get("Recovery_ServiceManagerNotStarted"));
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = string.Join(' ', new[] { await standardOutput, await standardError }
            .Where(value => !string.IsNullOrWhiteSpace(value)))
            .ReplaceLineEndings(" ")
            .Trim();
        return new(process.ExitCode, string.IsNullOrEmpty(output)
            ? Strings.Format("Recovery_ErrorCode", process.ExitCode)
            : output);
    }

    private sealed record ScResult(int ExitCode, string Message);
}
