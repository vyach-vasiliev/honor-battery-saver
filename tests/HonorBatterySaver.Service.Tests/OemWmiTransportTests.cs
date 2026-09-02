using HonorBatterySaver.Service;

namespace HonorBatterySaver.Service.Tests;

public sealed class OemWmiTransportTests
{
    [Fact]
    public void CreatesZeroPadded64ByteInputBuffer()
    {
        var buffer = OemWmiTransport.CreateInputBuffer([0x03, 0x10, 0x28, 0x46]);

        Assert.Equal(64, buffer.Length);
        Assert.Equal([0x03, 0x10, 0x28, 0x46], buffer[..4]);
        Assert.All(buffer[4..], value => Assert.Equal(0, value));
    }

    [Fact]
    public void RejectsCommandWithUnexpectedLength()
    {
        Assert.Throws<ArgumentException>(() => OemWmiTransport.CreateInputBuffer([0x03, 0x10, 0x28]));
    }

    [Theory]
    [InlineData(@"ACPI\PNP0C14\HWMI_0", true)]
    [InlineData(@"acpi\pnp0c14\hwmi_0", true)]
    [InlineData(@"ACPI\PNP0C14\HWMI_1", false)]
    public void SelectsOnlyConfirmedProviderInstance(string name, bool expected)
    {
        Assert.Equal(expected, OemWmiTransport.IsPreferredInstance(name));
    }

    [Theory]
    [InlineData(true, 1, true)]
    [InlineData(false, 0, false)]
    [InlineData(null, 0, true)]
    [InlineData(null, 1, false)]
    public void InterpretsScalarResultOrFallsBackToOemOutput(bool? scalarResult, byte firstOutputByte, bool expected)
    {
        Assert.Equal(expected, OemWmiTransport.IsSuccessfulResult(scalarResult, [firstOutputByte]));
    }

    [Fact]
    public void EmptyOemOutputIsNotSuccessfulWhenScalarResultIsMissing()
    {
        Assert.False(OemWmiTransport.IsSuccessfulResult(null, []));
    }
}
