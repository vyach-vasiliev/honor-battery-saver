using System.Text;

namespace HonorBatterySaver.Service;

public sealed class RotatingFileLog
{
    private const long MaximumBytes = 1024 * 1024;
    private const int RetainedFiles = 3;
    private readonly object _sync = new();
    private readonly string _path;

    public RotatingFileLog(string path) => _path = path;

    public void Write(string level, string message)
    {
        lock (_sync)
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                }

                RotateIfNeeded();
                File.AppendAllText(_path,
                    $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch
            {
                // Logging must never terminate the service.
            }
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_path) || new FileInfo(_path).Length < MaximumBytes)
        {
            return;
        }

        var oldest = $"{_path}.{RetainedFiles}";
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = RetainedFiles - 1; index >= 1; index--)
        {
            var source = $"{_path}.{index}";
            if (File.Exists(source))
            {
                File.Move(source, $"{_path}.{index + 1}");
            }
        }

        File.Move(_path, $"{_path}.1");
    }
}
