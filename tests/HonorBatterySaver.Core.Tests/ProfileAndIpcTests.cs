using HonorBatterySaver.Core;

namespace HonorBatterySaver.Core.Tests;

public sealed class ProfileAndIpcTests
{
    [Theory]
    [InlineData(BatteryMode.Home, 0, "03102846")]
    [InlineData(BatteryMode.Office, 1, "0310465A")]
    [InlineData(BatteryMode.Travel, 2, "03105F64")]
    public void ProfilesHaveExactPayloadAndRegistryValue(BatteryMode mode, int registryValue, string payload)
    {
        var profile = BatteryProfiles.Get(mode);

        Assert.Equal(registryValue, profile.RegistryValue);
        Assert.Equal(payload, Convert.ToHexString(profile.OemPayload));
        Assert.Equal(1, BatteryProfiles.EnabledRegistryStatus);
    }

    [Fact]
    public void RejectsArbitraryModeValue()
    {
        var request = new IpcRequest(IpcOperation.ApplyMode, (BatteryMode)42);
        Assert.NotNull(IpcRequestValidator.Validate(request));
    }

    [Fact]
    public void RejectsModeOnStatusOperation()
    {
        var request = new IpcRequest(IpcOperation.GetStatus, BatteryMode.Home);
        Assert.NotNull(IpcRequestValidator.Validate(request));
    }

    [Fact]
    public void GateSuppressesOnlyRepeatedSuccessfulCommandUnlessForced()
    {
        var gate = new ApplyCommandGate();

        Assert.True(gate.ShouldApply(BatteryMode.Home, false));
        Assert.True(gate.ShouldApply(BatteryMode.Home, false));
        gate.RecordSuccess(BatteryMode.Home);
        Assert.False(gate.ShouldApply(BatteryMode.Home, false));
        Assert.True(gate.ShouldApply(BatteryMode.Home, true));
        Assert.True(gate.ShouldApply(BatteryMode.Office, false));
    }
}
