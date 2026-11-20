using System;
using System.IO;
using System.Text;

namespace CommunityStats.Util;

/// <summary>
/// Crash-safe small-file persistence. JSON written here always goes to a
/// temporary file in the same directory, is flushed to disk, and only then
/// replaces the previous file. A failed replace falls back to a normal write
/// so a read-only or unusual filesystem does not lose the update entirely.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string content)
    {
        if (string.IsNullOrEmpty(path)) return;

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        var backup = path + ".bak";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, backup, ignoreMetadataErrors: true);
                    TryDelete(backup);
                }
                catch
                {
                    // Some filesystems do not support Replace. Move is still
                    // atomic on the same volume; if that also fails, write
                    // directly so the caller keeps the new data.
                    try
                    {
                        File.Move(temp, path, overwrite: true);
                    }
                    catch
                    {
                        File.WriteAllText(path, content, new UTF8Encoding(false));
                    }
                }
            }
            else
            {
                File.Move(temp, path);
            }
        }
        catch (Exception ex)
        {
            Safe.Warn($"[AtomicFile] write {Path.GetFileName(path)} failed: {ex.Message}");
            try { File.WriteAllText(path, content, new UTF8Encoding(false)); }
            catch (Exception fallbackEx)
            {
                Safe.Warn($"[AtomicFile] fallback write {Path.GetFileName(path)} failed: {fallbackEx.Message}");
            }
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>
    /// Move an unreadable JSON file aside instead of letting the next save
    /// overwrite the only copy. Returns the quarantine path when moved.
    /// </summary>
    public static string? Quarantine(string path, Exception cause)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var target = $"{path}.corrupt-{stamp}";
            if (File.Exists(target))
                target += "-" + Guid.NewGuid().ToString("N")[..6];
            File.Move(path, target);
            Safe.Warn($"[AtomicFile] quarantined corrupt {Path.GetFileName(path)} as {Path.GetFileName(target)}: {cause.GetType().Name}: {cause.Message}");
            return target;
        }
        catch (Exception ex)
        {
            Safe.Warn($"[AtomicFile] could not quarantine {Path.GetFileName(path)}: {ex.Message}");
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
