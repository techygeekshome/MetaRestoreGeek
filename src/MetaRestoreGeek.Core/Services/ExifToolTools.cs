using System.Diagnostics;

namespace MetaRestoreGeek.Core.Services;

/// <summary>
/// Finds exiftool. Same stance as VideoSizeGeek's FfmpegTools: no bundling, no silent download
/// of a binary from a URL baked into the app. exiftool is the one tool that reliably writes every
/// EXIF/XMP/IPTC field this app needs across every photo format Google Photos exports, and it is
/// widely packaged (winget, chocolatey, the author's own site), so pointing the user at it once
/// is a better long-term bet than vendoring a copy this app would then be responsible for updating.
/// </summary>
public static class ExifToolTools
{
    public const string OfficialSiteUrl = "https://exiftool.org/";

    public static string CacheDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TechyGeeksHome", "MetaRestoreGeek", "bin");

    private static string ExeName() => OperatingSystem.IsWindows() ? "exiftool.exe" : "exiftool";

    public static string? FindExifTool()
    {
        var exeName = ExeName();

        var cached = Path.Combine(CacheDirectory, exeName);
        if (File.Exists(cached) && Runs(cached))
            return cached;

        var onPath = FindOnPath(exeName);
        if (onPath is not null && Runs(onPath))
            return onPath;

        // The Windows build from exiftool.org ships as "exiftool(-k).exe" so double-clicking it
        // pauses for a keypress; people who rename it or leave it as-is both end up with a
        // working "exiftool.exe" once unzipped, but some leave the original name. Check for it
        // on PATH too so a manual install still gets picked up without asking the user to rename it.
        if (OperatingSystem.IsWindows())
        {
            var kVariant = FindOnPath("exiftool(-k).exe");
            if (kVariant is not null && Runs(kVariant))
                return kVariant;
        }

        return null;
    }

    private static string? FindOnPath(string exeName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, exeName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>A binary that exists is not proof it works. Actually run it.</summary>
    private static bool Runs(string exePath)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exePath, "-ver")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return false;
            if (!p.WaitForExit(5000)) { p.Kill(entireProcessTree: true); return false; }
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Registers a copy the user has pointed at manually, same shape as VideoSizeGeek's
    /// AdoptManualCopies, so later runs find it the same way they would a fetched one.</summary>
    public static void AdoptManualCopy(string exifToolPath)
    {
        if (!Runs(exifToolPath)) throw new InvalidOperationException("That exiftool does not run.");

        Directory.CreateDirectory(CacheDirectory);
        File.Copy(exifToolPath, Path.Combine(CacheDirectory, ExeName()), overwrite: true);
    }
}
