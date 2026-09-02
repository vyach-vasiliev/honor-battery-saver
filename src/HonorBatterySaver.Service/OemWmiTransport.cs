namespace HonorBatterySaver.Service;

internal static class OemWmiTransport
{
    internal const int CommandLength = 4;
    internal const int InputBufferLength = 64;
    private const string PreferredInstanceName = @"ACPI\PNP0C14\HWMI_0";

    internal static byte[] CreateInputBuffer(byte[] command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Length != CommandLength)
        {
            throw new ArgumentException($"OEM command must contain exactly {CommandLength} bytes.", nameof(command));
        }

        var buffer = new byte[InputBufferLength];
        command.CopyTo(buffer, 0);
        return buffer;
    }

    internal static bool IsPreferredInstance(string instanceName) =>
        string.Equals(instanceName, PreferredInstanceName, StringComparison.OrdinalIgnoreCase);

    internal static bool IsSuccessfulResult(object? scalarResult, byte[] output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return scalarResult switch
        {
            bool booleanResult => booleanResult,
            null => output is [0, ..],
            _ => Convert.ToBoolean(scalarResult)
        };
    }

    internal static string FormatScalarResult(object? scalarResult) => scalarResult switch
    {
        null => "<null>",
        bool value => value.ToString(),
        _ => scalarResult.GetType().Name
    };
}
