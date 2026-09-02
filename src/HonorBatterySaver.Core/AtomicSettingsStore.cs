using System.Text.Json;

namespace HonorBatterySaver.Core;

public sealed record SettingsLoadResult(AppSettings Settings, string? RecoveredBrokenFile, string? Warning);

public sealed class AtomicSettingsStore
{
    private readonly string _path;

    public AtomicSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return new(AppSettings.CreateDefault(), null, null);
        }

        try
        {
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonDefaults.Options, cancellationToken);
            return new(SettingsBehavior.Normalize(settings), null, null);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            var brokenPath = GetBrokenPath();
            File.Move(_path, brokenPath);
            return new(AppSettings.CreateDefault(), brokenPath, Strings.Get("Settings_CorruptRecovered"));
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = SettingsBehavior.Normalize(settings.Clone());
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Settings path must have a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = _path + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonDefaults.Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(_path))
            {
                File.Replace(temporaryPath, _path, null);
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string GetBrokenPath()
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
        var candidate = $"{_path}.broken.{timestamp}";
        var suffix = 0;
        while (File.Exists(candidate))
        {
            candidate = $"{_path}.broken.{timestamp}.{++suffix}";
        }

        return candidate;
    }
}
