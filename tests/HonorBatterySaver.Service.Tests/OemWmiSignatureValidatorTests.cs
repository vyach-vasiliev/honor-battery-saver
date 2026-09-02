using HonorBatterySaver.Service;

namespace HonorBatterySaver.Service.Tests;

public sealed class OemWmiSignatureValidatorTests
{
    private const string CompatibleMof = """
        [dynamic: ToInstance, provider("WMIProv"), WMI,
         GUID("{ABBC0f5b-8ea1-11d1-A000-c90629100000}")]
        class OemWMIMethod
        {
            [WmiMethodId(1), Implemented] void OemWMIfun(
                [in, MAX(64)] uint8 u8Input[],
                [out] uint32 u32Resrved,
                [out, MAX(256)] uint8 u8Output[]);
        };
        """;

    [Fact]
    public void AcceptsConfirmedHonorMofSignature()
    {
        Assert.True(OemWmiSignatureValidator.IsCompatibleMof(CompatibleMof));
    }

    [Fact]
    public void RejectsUnexpectedProviderGuid()
    {
        Assert.False(OemWmiSignatureValidator.IsCompatibleMof(
            CompatibleMof.Replace("ABBC0f5b", "00000000", StringComparison.Ordinal)));
    }

    [Fact]
    public void RejectsMissingOutputParameter()
    {
        Assert.False(OemWmiSignatureValidator.IsCompatibleMof(
            CompatibleMof.Replace("[out, MAX(256)] uint8 u8Output[]", string.Empty, StringComparison.Ordinal)));
    }
}
