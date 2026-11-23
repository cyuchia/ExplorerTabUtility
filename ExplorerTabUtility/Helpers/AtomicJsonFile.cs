using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ExplorerTabUtility.Helpers;

// Callers serialize access. Temp and destination share a volume so replacement is atomic.
public static class AtomicJsonFile
{
    public static T Load<T>(string path, out string? recovery) where T : new()
    {
        recovery = null;
        if (!File.Exists(path) && !File.Exists(path + ".bak")) return new T();
        try { return Read<T>(path); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            try
            {
                var restored = Read<T>(path + ".bak");
                // Preserve the failed file before the next save; never replace a good backup with it.
                if (File.Exists(path)) File.Move(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
                recovery = "backup";
                return restored;
            }
            catch (Exception backupError) when (backupError is IOException or JsonException or UnauthorizedAccessException)
            {
                // Fail closed: do not silently replace unreadable settings with defaults.
                throw new IOException("Settings and backup could not be loaded.", new AggregateException(ex, backupError));
            }
        }
    }

    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path))
        ?? throw new JsonException("Empty settings document.");

    public static void Save(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
