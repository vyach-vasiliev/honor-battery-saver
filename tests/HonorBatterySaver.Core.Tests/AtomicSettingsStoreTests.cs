using System.Text.Json;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Core.Tests;

public sealed class AtomicSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "HonorBatterySaver.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RoundTripsSettingsAndPreservesSignificantSsidWhitespace()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = Path.Combine(_directory, "settings.json");
        var store = new AtomicSettingsStore(path);
        var settings = AppSettings.CreateDefault();
        settings.Language = UiLanguage.English;
        settings.NetworkRules.Add(new NetworkRule { Ssid = " Home ", Mode = BatteryMode.Home });

        await store.SaveAsync(settings, cancellationToken);
        var loaded = await store.LoadAsync(cancellationToken);

        Assert.Equal(" Home ", Assert.Single(loaded.Settings.NetworkRules).Ssid);
        Assert.Equal(UiLanguage.English, loaded.Settings.Language);
        Assert.Null(loaded.Warning);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task MigratesOldSchemaToCurrentVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":0,\"automaticMode\":true}", cancellationToken);

        var result = await new AtomicSettingsStore(path).LoadAsync(cancellationToken);

        Assert.Equal(AppSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);
        Assert.Equal(BatteryMode.Travel, result.Settings.DefaultMode);
        Assert.Equal(UiLanguage.System, result.Settings.Language);
    }

    [Fact]
    public async Task BacksUpCorruptSettingsAndRestoresSafeDefaults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, "{ definitely-not-json", cancellationToken);

        var result = await new AtomicSettingsStore(path).LoadAsync(cancellationToken);

        Assert.NotNull(result.RecoveredBrokenFile);
        Assert.True(File.Exists(result.RecoveredBrokenFile));
        Assert.True(result.Settings.AutomaticMode);
        Assert.Equal(BatteryMode.Travel, result.Settings.DefaultMode);
        Assert.NotNull(result.Warning);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
