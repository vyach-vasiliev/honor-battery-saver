using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Tray;

public sealed record WifiSnapshot(IReadOnlyList<string> Ssids, bool AccessDenied, string Message);
public sealed record WifiCatalogSnapshot(
    IReadOnlyList<WifiNetworkCandidate> Networks,
    bool AccessDenied,
    string Message);

public sealed class NativeWifiProvider
{
    public WifiCatalogSnapshot GetNetworkCatalog()
    {
        IntPtr clientHandle = IntPtr.Zero;
        IntPtr interfaceList = IntPtr.Zero;
        try
        {
            var result = WlanOpenHandle(2, IntPtr.Zero, out _, out clientHandle);
            if (result != 0)
            {
                return CatalogFailure(result);
            }

            result = WlanEnumInterfaces(clientHandle, IntPtr.Zero, out interfaceList);
            if (result != 0)
            {
                return CatalogFailure(result);
            }

            var candidates = new List<WifiNetworkCandidate>();
            var count = Marshal.ReadInt32(interfaceList, 0);
            var itemSize = Marshal.SizeOf<WlanInterfaceInfo>();
            var itemPointer = IntPtr.Add(interfaceList, 8);
            for (var index = 0; index < count; index++)
            {
                var info = Marshal.PtrToStructure<WlanInterfaceInfo>(IntPtr.Add(itemPointer, index * itemSize));
                uint queryResult = 0;
                var connectedSsid = info.State == WlanInterfaceState.Connected
                    ? GetCurrentSsid(clientHandle, info.InterfaceGuid, out queryResult)
                    : null;
                if (info.State == WlanInterfaceState.Connected && queryResult == 5)
                {
                    return CatalogFailure(queryResult);
                }

                if (!string.IsNullOrEmpty(connectedSsid))
                {
                    candidates.Add(new(connectedSsid, true, true, true));
                }

                result = AddAvailableNetworks(clientHandle, info.InterfaceGuid, connectedSsid, candidates);
                if (result == 5)
                {
                    return CatalogFailure(result);
                }

                result = AddKnownNetworks(clientHandle, info.InterfaceGuid, candidates);
                if (result == 5)
                {
                    return CatalogFailure(result);
                }
            }

            var ordered = WifiNetworkCatalog.Order(candidates);
            return new(ordered, false, ordered.Count == 0
                ? Strings.Get("Wifi_NoCatalogNetworks")
                : Strings.Format("Wifi_CatalogCount", ordered.Count));
        }
        catch (Exception exception) when (exception is Win32Exception or ExternalException or XmlException)
        {
            return new([], false, Strings.Format("Wifi_CatalogFailed", exception.GetType().Name));
        }
        finally
        {
            if (interfaceList != IntPtr.Zero)
            {
                WlanFreeMemory(interfaceList);
            }

            if (clientHandle != IntPtr.Zero)
            {
                WlanCloseHandle(clientHandle, IntPtr.Zero);
            }
        }
    }

    public WifiSnapshot GetConnectedNetworks()
    {
        IntPtr clientHandle = IntPtr.Zero;
        IntPtr interfaceList = IntPtr.Zero;
        try
        {
            var result = WlanOpenHandle(2, IntPtr.Zero, out _, out clientHandle);
            if (result != 0)
            {
                return Failure(result);
            }

            result = WlanEnumInterfaces(clientHandle, IntPtr.Zero, out interfaceList);
            if (result != 0)
            {
                return Failure(result);
            }

            var count = Marshal.ReadInt32(interfaceList, 0);
            var itemSize = Marshal.SizeOf<WlanInterfaceInfo>();
            var itemPointer = IntPtr.Add(interfaceList, 8);
            var ssids = new List<string>();
            for (var index = 0; index < count; index++)
            {
                var info = Marshal.PtrToStructure<WlanInterfaceInfo>(IntPtr.Add(itemPointer, index * itemSize));
                if (info.State != WlanInterfaceState.Connected)
                {
                    continue;
                }

                var ssid = GetCurrentSsid(clientHandle, info.InterfaceGuid, out var queryResult);
                if (queryResult == 5)
                {
                    return Failure(queryResult);
                }

                if (!string.IsNullOrEmpty(ssid) && !ssids.Contains(ssid, StringComparer.Ordinal))
                {
                    ssids.Add(ssid);
                }
            }

            return new WifiSnapshot(ssids, false, ssids.Count == 0
                ? Strings.Get("Wifi_NotConnected")
                : Strings.Get("Wifi_AccessAllowed"));
        }
        catch (Exception exception) when (exception is Win32Exception or ExternalException)
        {
            return new WifiSnapshot([], false, Strings.Format("Wifi_DetectFailed", exception.GetType().Name));
        }
        finally
        {
            if (interfaceList != IntPtr.Zero)
            {
                WlanFreeMemory(interfaceList);
            }

            if (clientHandle != IntPtr.Zero)
            {
                WlanCloseHandle(clientHandle, IntPtr.Zero);
            }
        }
    }

    private static string? GetCurrentSsid(IntPtr clientHandle, Guid interfaceGuid, out uint queryResult)
    {
        IntPtr data = IntPtr.Zero;
        queryResult = WlanQueryInterface(
            clientHandle,
            ref interfaceGuid,
            WlanIntfOpcode.CurrentConnection,
            IntPtr.Zero,
            out _,
            out data,
            out _);
        if (queryResult != 0)
        {
            return null;
        }

        try
        {
            var attributes = Marshal.PtrToStructure<WlanConnectionAttributes>(data);
            var length = (int)Math.Min(attributes.AssociationAttributes.Ssid.Length, 32u);
            return length == 0 ? null : Encoding.UTF8.GetString(attributes.AssociationAttributes.Ssid.Value, 0, length);
        }
        finally
        {
            WlanFreeMemory(data);
        }
    }

    private static uint AddAvailableNetworks(
        IntPtr clientHandle,
        Guid interfaceGuid,
        string? connectedSsid,
        ICollection<WifiNetworkCandidate> candidates)
    {
        IntPtr networkList = IntPtr.Zero;
        var result = WlanGetAvailableNetworkList(clientHandle, ref interfaceGuid, 0, IntPtr.Zero, out networkList);
        if (result != 0)
        {
            return result;
        }

        try
        {
            var count = Marshal.ReadInt32(networkList, 0);
            var itemSize = Marshal.SizeOf<WlanAvailableNetwork>();
            var itemPointer = IntPtr.Add(networkList, 8);
            for (var index = 0; index < count; index++)
            {
                var network = Marshal.PtrToStructure<WlanAvailableNetwork>(IntPtr.Add(itemPointer, index * itemSize));
                var ssid = DecodeSsid(network.Ssid);
                if (string.IsNullOrEmpty(ssid))
                {
                    continue;
                }

                var flags = (WlanAvailableNetworkFlags)network.Flags;
                candidates.Add(new WifiNetworkCandidate(
                    ssid,
                    true,
                    flags.HasFlag(WlanAvailableNetworkFlags.Connected)
                        || string.Equals(ssid, connectedSsid, StringComparison.Ordinal),
                    flags.HasFlag(WlanAvailableNetworkFlags.HasProfile)
                        || !string.IsNullOrEmpty(network.ProfileName)));
            }

            return 0;
        }
        finally
        {
            WlanFreeMemory(networkList);
        }
    }

    private static uint AddKnownNetworks(
        IntPtr clientHandle,
        Guid interfaceGuid,
        ICollection<WifiNetworkCandidate> candidates)
    {
        IntPtr profileList = IntPtr.Zero;
        var result = WlanGetProfileList(clientHandle, ref interfaceGuid, IntPtr.Zero, out profileList);
        if (result != 0)
        {
            return result;
        }

        try
        {
            var count = Marshal.ReadInt32(profileList, 0);
            var itemSize = Marshal.SizeOf<WlanProfileInfo>();
            var itemPointer = IntPtr.Add(profileList, 8);
            for (var index = 0; index < count; index++)
            {
                var profile = Marshal.PtrToStructure<WlanProfileInfo>(IntPtr.Add(itemPointer, index * itemSize));
                var ssid = GetProfileSsid(clientHandle, interfaceGuid, profile.ProfileName);
                if (!string.IsNullOrEmpty(ssid))
                {
                    candidates.Add(new WifiNetworkCandidate(ssid, false, false, true));
                }
            }

            return 0;
        }
        finally
        {
            WlanFreeMemory(profileList);
        }
    }

    private static string? GetProfileSsid(IntPtr clientHandle, Guid interfaceGuid, string profileName)
    {
        IntPtr profileXml = IntPtr.Zero;
        uint flags = 0;
        var result = WlanGetProfile(
            clientHandle,
            ref interfaceGuid,
            profileName,
            IntPtr.Zero,
            out profileXml,
            ref flags,
            out _);
        if (result != 0)
        {
            return null;
        }

        try
        {
            var xml = Marshal.PtrToStringUni(profileXml);
            if (string.IsNullOrEmpty(xml))
            {
                return null;
            }

            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            var document = XDocument.Load(xmlReader, LoadOptions.None);
            var ssidElement = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "SSID");
            var name = ssidElement?.Elements().FirstOrDefault(element => element.Name.LocalName == "name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            var hex = ssidElement?.Elements().FirstOrDefault(element => element.Name.LocalName == "hex")?.Value;
            return string.IsNullOrEmpty(hex) ? null : Encoding.UTF8.GetString(Convert.FromHexString(hex));
        }
        catch (FormatException)
        {
            return null;
        }
        finally
        {
            WlanFreeMemory(profileXml);
        }
    }

    private static string? DecodeSsid(Dot11Ssid ssid)
    {
        var length = (int)Math.Min(ssid.Length, 32u);
        return length == 0 ? null : Encoding.UTF8.GetString(ssid.Value, 0, length);
    }

    private static WifiSnapshot Failure(uint error) => error == 5
        ? new([], true, Strings.Get("Wifi_NameAccessDenied"))
        : new([], false, Strings.Format("Wifi_DetectWindowsError", error));

    private static WifiCatalogSnapshot CatalogFailure(uint error) => error == 5
        ? new([], true, Strings.Get("Wifi_ListAccessDenied"))
        : new([], false, Strings.Format("Wifi_ListWindowsError", error));

    private enum WlanInterfaceState
    {
        NotReady,
        Connected,
        AdHocNetworkFormed,
        Disconnecting,
        Disconnected,
        Associating,
        Discovering,
        Authenticating
    }

    private enum WlanConnectionMode
    {
        Profile,
        TemporaryProfile,
        DiscoverySecure,
        DiscoveryUnsecure,
        Auto,
        Invalid
    }

    private enum WlanIntfOpcode
    {
        AutoconfEnabled = 1,
        BackgroundScanEnabled,
        MediaStreamingMode,
        RadioState,
        BssType,
        InterfaceState,
        CurrentConnection
    }

    [Flags]
    private enum WlanAvailableNetworkFlags : uint
    {
        Connected = 1,
        HasProfile = 2
    }

#pragma warning disable CS0649
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanInterfaceInfo
    {
        public Guid InterfaceGuid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Description;

        public WlanInterfaceState State;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanConnectionAttributes
    {
        public WlanInterfaceState InterfaceState;
        public WlanConnectionMode ConnectionMode;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ProfileName;

        public WlanAssociationAttributes AssociationAttributes;
        public WlanSecurityAttributes SecurityAttributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Dot11Ssid
    {
        public uint Length;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanAssociationAttributes
    {
        public Dot11Ssid Ssid;
        public int BssType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] Bssid;

        public int PhyType;
        public uint PhyIndex;
        public uint SignalQuality;
        public uint RxRate;
        public uint TxRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanSecurityAttributes
    {
        [MarshalAs(UnmanagedType.Bool)] public bool SecurityEnabled;
        [MarshalAs(UnmanagedType.Bool)] public bool OneXEnabled;
        public int AuthAlgorithm;
        public int CipherAlgorithm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanAvailableNetwork
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ProfileName;

        public Dot11Ssid Ssid;
        public int BssType;
        public uint NumberOfBssids;

        [MarshalAs(UnmanagedType.Bool)]
        public bool NetworkConnectable;

        public uint NotConnectableReason;
        public uint NumberOfPhyTypes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public int[] PhyTypes;

        [MarshalAs(UnmanagedType.Bool)]
        public bool MorePhyTypes;

        public uint SignalQuality;

        [MarshalAs(UnmanagedType.Bool)]
        public bool SecurityEnabled;

        public int DefaultAuthAlgorithm;
        public int DefaultCipherAlgorithm;
        public uint Flags;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanProfileInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ProfileName;

        public uint Flags;
    }
#pragma warning restore CS0649

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(
        uint clientVersion,
        IntPtr reserved,
        out uint negotiatedVersion,
        out IntPtr clientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr clientHandle, IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr clientHandle, IntPtr reserved, out IntPtr interfaceList);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        WlanIntfOpcode opcode,
        IntPtr reserved,
        out uint dataSize,
        out IntPtr data,
        out int opcodeValueType);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        uint flags,
        IntPtr reserved,
        out IntPtr availableNetworkList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetProfileList(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        IntPtr reserved,
        out IntPtr profileList);

    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanGetProfile(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        string profileName,
        IntPtr reserved,
        out IntPtr profileXml,
        ref uint flags,
        out uint grantedAccess);
}
