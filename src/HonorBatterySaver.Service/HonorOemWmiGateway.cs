using System.Management;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Service;

public sealed class HonorOemWmiGateway(RotatingFileLog log) : IOemWmiGateway
{
    private const string WmiNamespace = @"ROOT\WMI";
    private const string ClassName = "OemWMIMethod";
    private const string MethodName = "OemWMIfun";

    public Task<WmiProbe> ProbeAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var managementClass = CreateClass();
            managementClass.Get();
            var method = managementClass.Methods[MethodName];
            if (method is null || !HasCompatibleSignature(managementClass, method))
            {
                return new WmiProbe(false, Strings.Get("Wmi_MethodMissing"));
            }

            using var instances = managementClass.GetInstances();
            var instanceSummary = InspectInstances(instances);
            log.Write("INFO", $"OEM WMI probe: instances={instanceSummary.Total}; active={instanceSummary.Active}; hwmi0={instanceSummary.Preferred}.");
            if (instanceSummary.Total == 0)
            {
                return new WmiProbe(false, Strings.Get("Wmi_InstanceMissing"));
            }

            if (instanceSummary.Active == 0)
            {
                return new WmiProbe(false, Strings.Get("Wmi_ActiveInstanceMissing"));
            }

            if (instanceSummary.Preferred != 1)
            {
                return new WmiProbe(false, Strings.Get("Wmi_HwmiInstanceMissing"));
            }

            return new WmiProbe(true, Strings.Get("Wmi_Available"));
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return new WmiProbe(false, ToSafeMessage(exception));
        }
    }, cancellationToken);

    public Task<WmiInvocation> InvokeAsync(byte[] payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var managementClass = CreateClass();
            managementClass.Get();
            var method = managementClass.Methods[MethodName]
                ?? throw new InvalidOperationException("OEM WMI method is missing.");
            if (!HasCompatibleSignature(managementClass, method))
            {
                throw new InvalidOperationException("OEM WMI method signature is incompatible.");
            }

            using var instances = managementClass.GetInstances();
            var instance = instances.Cast<ManagementObject>()
                .SingleOrDefault(instance => IsActive(instance) && IsPreferred(instance))
                ?? throw new InvalidOperationException("Active OEM WMI HWMI_0 provider instance is missing.");
            using (instance)
            {
                // The positional overload avoids a firmware-provider compatibility issue where
                // OutParameters/GetMethodParameters metadata is exposed but rejected on invoke.
                object[] arguments = [OemWmiTransport.CreateInputBuffer(payload), null!, null!];
                var returnValue = instance.InvokeMethod(MethodName, arguments);
                if (arguments[1] is null || arguments[2] is not byte[] output)
                {
                    throw new InvalidOperationException("OEM WMI returned incompatible output parameters.");
                }

                log.Write("INFO", $"OEM WMI invoke completed: scalar={OemWmiTransport.FormatScalarResult(returnValue)}; outputLength={output.Length}.");

                return new WmiInvocation(
                    OemWmiTransport.IsSuccessfulResult(returnValue, output),
                    Convert.ToUInt32(arguments[1]),
                    output);
            }
        }, cancellationToken);
    }

    private static (int Total, int Active, int Preferred) InspectInstances(ManagementObjectCollection instances)
    {
        var total = 0;
        var active = 0;
        var preferred = 0;
        foreach (ManagementObject instance in instances)
        {
            using (instance)
            {
                total++;
                if (IsActive(instance))
                {
                    active++;
                    if (IsPreferred(instance))
                    {
                        preferred++;
                    }
                }
            }
        }

        return (total, active, preferred);
    }

    private static bool IsActive(ManagementObject instance) =>
        instance["Active"] is true;

    private static bool IsPreferred(ManagementObject instance) =>
        instance["InstanceName"] is string name && OemWmiTransport.IsPreferredInstance(name);

    private static ManagementClass CreateClass()
    {
        var scope = new ManagementScope($@"\\.\{WmiNamespace}", new ConnectionOptions
        {
            EnablePrivileges = true,
            Authentication = AuthenticationLevel.PacketPrivacy,
            Impersonation = ImpersonationLevel.Impersonate
        });
        scope.Connect();
        return new ManagementClass(scope, new ManagementPath(ClassName), null);
    }

    private static bool HasCompatibleSignature(ManagementClass managementClass, MethodData method)
    {
        var input = method.InParameters?.Properties["u8Input"];
        if (input is not { Type: CimType.UInt8, IsArray: true })
        {
            return false;
        }

        try
        {
            var reserved = method.OutParameters?.Properties["u32Resrved"];
            var output = method.OutParameters?.Properties["u8Output"];
            var returnValue = method.OutParameters?.Properties["ReturnValue"];
            return reserved is { Type: CimType.UInt32, IsArray: false }
                && output is { Type: CimType.UInt8, IsArray: true }
                && returnValue is { Type: CimType.Boolean, IsArray: false };
        }
        catch (ManagementException exception) when (exception.ErrorCode == ManagementStatus.NotFound)
        {
            return OemWmiSignatureValidator.IsCompatibleMof(managementClass.GetText(TextFormat.Mof));
        }
    }

    private static string ToSafeMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => Strings.Get("Wmi_AccessDenied"),
        ManagementException managementException when managementException.ErrorCode == ManagementStatus.AccessDenied =>
            Strings.Get("Wmi_RunServiceAsAdmin"),
        ManagementException managementException when managementException.ErrorCode is ManagementStatus.InvalidClass or ManagementStatus.NotFound =>
            Strings.Get("Wmi_ClassMissing"),
        ManagementException managementException when managementException.ErrorCode == ManagementStatus.InvalidNamespace =>
            Strings.Get("Wmi_NamespaceUnavailable"),
        _ => Strings.Format("Wmi_UnavailableError", exception.GetType().Name)
    };
}
