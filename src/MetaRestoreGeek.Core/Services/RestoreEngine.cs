using System.Diagnostics;
using System.Globalization;
using MetaRestoreGeek.Core.Models;

namespace MetaRestoreGeek.Core.Services;

public sealed record RestoreProgress(int Done, int Total, string CurrentFile);

/// <summary>
/// Ties SidecarMatcher, TakeoutJsonParser and exiftool together: for each photo in a folder,
/// find its sidecar, parse it, and write the recovered metadata into a NEW file. Nothing in this
/// class ever opens the original photo for writing; every original is left exactly as it was
/// found, which matters a lot the one time a match turns out to be wrong.
/// </summary>
public static class RestoreEngine
{
    // Extensions Google Takeout actually exports photos and videos as. Anything else in the
    // folder (the .json sidecars themselves, .html, whatever Google adds) is not a candidate.
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".gif", ".webp", ".mp4", ".mov", ".avi", ".mkv"
    };

    public static async Task<IReadOnlyList<RestoreOutcome>> RestoreFolderAsync(
        string exiftoolPath,
        string sourceFolder,
        string outputFolder,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(sourceFolder))
            throw new DirectoryNotFoundException(sourceFolder);

        Directory.CreateDirectory(outputFolder);

        var allNames = Directory.GetFiles(sourceFolder).Select(Path.GetFileName).Cast<string>().ToList();
        var photoNames = allNames.Where(n => MediaExtensions.Contains(Path.GetExtension(n))).OrderBy(n => n).ToList();

        var results = new List<RestoreOutcome>(photoNames.Count);
        for (var i = 0; i < photoNames.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var photoName = photoNames[i];
            progress?.Report(new RestoreProgress(i, photoNames.Count, photoName));

            var sourcePhotoPath = Path.Combine(sourceFolder, photoName);
            var outcome = await RestoreOneAsync(exiftoolPath, sourcePhotoPath, sourceFolder, outputFolder, allNames, ct);
            results.Add(outcome);
        }

        progress?.Report(new RestoreProgress(photoNames.Count, photoNames.Count, ""));
        return results;
    }

    private static async Task<RestoreOutcome> RestoreOneAsync(
        string exiftoolPath,
        string sourcePhotoPath,
        string sourceFolder,
        string outputFolder,
        IReadOnlyCollection<string> allNamesInFolder,
        CancellationToken ct)
    {
        var photoName = Path.GetFileName(sourcePhotoPath);
        var match = SidecarMatcher.Match(photoName, allNamesInFolder);

        if (!match.Found)
            return new RestoreOutcome(sourcePhotoPath, null, false, "No matching Takeout sidecar found.");

        PhotoMetadata metadata;
        try
        {
            var json = await File.ReadAllTextAsync(Path.Combine(sourceFolder, match.SidecarFileName!), ct);
            metadata = TakeoutJsonParser.Parse(json, photoName);
        }
        catch (Exception ex)
        {
            return new RestoreOutcome(sourcePhotoPath, null, false, $"Could not read sidecar: {ex.Message}");
        }

        var outputPath = Path.Combine(outputFolder, photoName);
        try
        {
            // The copy happens before exiftool ever runs, so a failed or interrupted write
            // leaves the source untouched and the output either absent or a plain, unedited copy,
            // never a half-written original.
            File.Copy(sourcePhotoPath, outputPath, overwrite: true);
        }
        catch (Exception ex)
        {
            return new RestoreOutcome(sourcePhotoPath, null, false, $"Could not create output copy: {ex.Message}");
        }

        var args = BuildExifToolArgs(metadata);
        if (args.Count == 0)
            return new RestoreOutcome(sourcePhotoPath, outputPath, true, "Sidecar matched, but had no date or location worth writing.");

        var exitCode = await RunExifToolAsync(exiftoolPath, outputPath, args, ct);
        return exitCode == 0
            ? new RestoreOutcome(sourcePhotoPath, outputPath, true, DescribeWhatWasWritten(metadata, match.Strategy))
            : new RestoreOutcome(sourcePhotoPath, outputPath, false, $"exiftool exited with code {exitCode}.");
    }

    /// <summary>
    /// Turns a parsed sidecar into exiftool command-line arguments. No process, no file I/O, so
    /// every date/timezone/hemisphere edge case can be proven with a plain assertion.
    /// </summary>
    public static IReadOnlyList<string> BuildExifToolArgs(PhotoMetadata metadata)
    {
        var args = new List<string>();

        // Takeout's timestamp is a true Unix second count, so it is exact UTC. exiftool's
        // classic DateTimeOriginal/CreateDate/ModifyDate tags carry no timezone of their own;
        // writing the UTC wall-clock into them is the same trade every other Takeout-restore
        // tool makes; OffsetTimeOriginal alongside it at least records that the time is UTC for
        // anything that reads the newer EXIF 2.31 offset tags.
        var when = metadata.PhotoTakenTime ?? metadata.CreationTime;
        if (when is { } dto)
        {
            var utc = dto.ToUniversalTime();
            args.Add($"-AllDates={utc:yyyy:MM:dd HH:mm:ss}");
            args.Add("-OffsetTimeOriginal=+00:00");
        }

        if (metadata.Geo is { IsMeaningful: true } geo)
        {
            args.Add($"-GPSLatitude={Math.Abs(geo.Latitude).ToString(CultureInfo.InvariantCulture)}");
            args.Add($"-GPSLatitudeRef={(geo.Latitude >= 0 ? "N" : "S")}");
            args.Add($"-GPSLongitude={Math.Abs(geo.Longitude).ToString(CultureInfo.InvariantCulture)}");
            args.Add($"-GPSLongitudeRef={(geo.Longitude >= 0 ? "E" : "W")}");

            if (geo.AltitudeMetres is { } alt && Math.Abs(alt) > 0.01)
            {
                args.Add($"-GPSAltitude={Math.Abs(alt).ToString(CultureInfo.InvariantCulture)}");
                args.Add($"-GPSAltitudeRef={(alt >= 0 ? "0" : "1")}");
            }
        }

        if (!string.IsNullOrWhiteSpace(metadata.Description))
        {
            // ImageDescription is the one caption field essentially every viewer reads.
            args.Add($"-ImageDescription={metadata.Description}");
        }

        return args;
    }

    private static async Task<int> RunExifToolAsync(string exiftoolPath, string targetFile, IReadOnlyList<string> tagArgs, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exiftoolPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in tagArgs)
            psi.ArgumentList.Add(a);

        // The copy is what exiftool edits, and -overwrite_original tells it to edit that copy in
        // place rather than leaving its own "file.jpg_original" backup next to it, which would
        // otherwise be a second, metadata-less copy sitting in the output folder confusing things.
        psi.ArgumentList.Add("-overwrite_original");
        psi.ArgumentList.Add(targetFile);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start exiftool.");
        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }

    private static string DescribeWhatWasWritten(PhotoMetadata metadata, MatchStrategy strategy)
    {
        var parts = new List<string>();
        if (metadata.PhotoTakenTime is not null || metadata.CreationTime is not null) parts.Add("date taken");
        if (metadata.Geo is { IsMeaningful: true }) parts.Add("GPS location");
        if (!string.IsNullOrWhiteSpace(metadata.Description)) parts.Add("description");

        var what = parts.Count > 0 ? string.Join(", ", parts) : "nothing (sidecar was empty)";
        var how = strategy == MatchStrategy.Exact ? "" : $" (matched via {strategy})";
        return $"Restored {what}{how}.";
    }
}
