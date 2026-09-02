using System.Text.RegularExpressions;

namespace HonorBatterySaver.Service;

public static partial class OemWmiSignatureValidator
{
    public static bool IsCompatibleMof(string? mof)
    {
        if (string.IsNullOrWhiteSpace(mof)
            || !ClassPattern().IsMatch(mof)
            || !GuidPattern().IsMatch(mof))
        {
            return false;
        }

        var method = MethodPattern().Match(mof);
        if (!method.Success)
        {
            return false;
        }

        var parameters = method.Groups["parameters"].Value;
        return InputPattern().IsMatch(parameters)
            && ReservedPattern().IsMatch(parameters)
            && OutputPattern().IsMatch(parameters);
    }

    [GeneratedRegex(@"\bclass\s+OemWMIMethod\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"GUID\s*\(\s*\""\{ABBC0F5B-8EA1-11D1-A000-C90629100000\}\""\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"\b(?:void|boolean)\s+OemWMIfun\s*\((?<parameters>[^;]*)\)\s*;",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex MethodPattern();

    [GeneratedRegex(@"\[\s*in(?:\s*,[^\]]*)?\]\s*uint8\s+u8Input\s*\[\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputPattern();

    [GeneratedRegex(@"\[\s*out(?:\s*,[^\]]*)?\]\s*uint32\s+u32Resrved\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedPattern();

    [GeneratedRegex(@"\[\s*out(?:\s*,[^\]]*)?\]\s*uint8\s+u8Output\s*\[\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OutputPattern();
}
