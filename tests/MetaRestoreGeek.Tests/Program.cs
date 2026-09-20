using MetaRestoreGeek.Core.Models;
using MetaRestoreGeek.Core.Services;

// A plain console program rather than a test framework, so `dotnet run` proves the build on any
// machine with nothing installed. Same shape as the checks in VideoSizeGeek and CutGeek.

var failures = 0;

void Check(string name, bool ok, string? detail = null)
{
    Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail is null ? "" : $"  ({detail})"));
    if (!ok) failures++;
}

void Skip(string name, string why) => Console.WriteLine($"SKIP  {name} ({why})");

// ---------------------------------------------------------------- SidecarMatcher: the plain case

{
    var files = new[] { "IMG_1234.jpg", "IMG_1234.jpg.json", "other.jpg" };
    var match = SidecarMatcher.Match("IMG_1234.jpg", files);
    Check("exact match found", match.Found);
    Check("exact match uses the Exact strategy", match.Strategy == MatchStrategy.Exact, match.Strategy.ToString());
}

// ---------------------------------------------------------------- SidecarMatcher: supplemental-metadata suffix

{
    var files = new[] { "IMG_5555.jpg", "IMG_5555.jpg.supplemental-metadata.json" };
    var match = SidecarMatcher.Match("IMG_5555.jpg", files);
    Check("supplemental suffix match found", match.Found);
    Check("supplemental match uses the SupplementalSuffix strategy", match.Strategy == MatchStrategy.SupplementalSuffix);
}

// ---------------------------------------------------------------- SidecarMatcher: edited copy shares the original's sidecar

{
    var files = new[] { "IMG_9001-edited.jpg", "IMG_9001.jpg.json" };
    var match = SidecarMatcher.Match("IMG_9001-edited.jpg", files);
    Check("edited copy matches the original's sidecar", match.Found);
    Check("edited match uses the EditedSuffixStripped strategy", match.Strategy == MatchStrategy.EditedSuffixStripped);
    Check("edited match points at the original's json, not a nonexistent one",
        match.SidecarFileName == "IMG_9001.jpg.json", match.SidecarFileName ?? "null");
}

// ---------------------------------------------------------------- SidecarMatcher: numbered duplicate, counter moves after the extension

{
    // This is the case a naive matcher gets wrong: the photo is "IMG_2000(1).jpg" but its
    // sidecar is "IMG_2000.jpg(1).json", counter after the extension instead of before it.
    var files = new[] { "IMG_2000.jpg", "IMG_2000.jpg.json", "IMG_2000(1).jpg", "IMG_2000.jpg(1).json" };
    var match = SidecarMatcher.Match("IMG_2000(1).jpg", files);
    Check("numbered duplicate matches its reordered sidecar", match.Found);
    Check("numbered duplicate uses the NumberedDuplicate strategy", match.Strategy == MatchStrategy.NumberedDuplicate);
    Check("numbered duplicate does not match the first copy's sidecar",
        match.SidecarFileName == "IMG_2000.jpg(1).json", match.SidecarFileName ?? "null");

    var firstCopyMatch = SidecarMatcher.Match("IMG_2000.jpg", files);
    Check("the first copy still matches its own plain sidecar, not the duplicate's",
        firstCopyMatch.SidecarFileName == "IMG_2000.jpg.json", firstCopyMatch.SidecarFileName ?? "null");
}

// ---------------------------------------------------------------- SidecarMatcher: truncated long filename, best effort

{
    var longName = "a_very_long_filename_that_google_takeout_will_truncate_somewhere.jpg";
    var truncatedJson = "a_very_long_filename_that_google_takeout_will_trunc.json";
    var files = new[] { longName, truncatedJson };
    var match = SidecarMatcher.Match(longName, files);
    Check("truncated prefix sidecar found", match.Found);
    Check("truncated match uses the TruncatedPrefix strategy", match.Strategy == MatchStrategy.TruncatedPrefix);
}

Check("no match when nothing in the folder fits",
    !SidecarMatcher.Match("orphan.jpg", new[] { "unrelated.json" }).Found);

// ---------------------------------------------------------------- TakeoutJsonParser

{
    const string sampleJson = """
    {
      "title": "IMG_1234.jpg",
      "description": "A nice photo",
      "photoTakenTime": { "timestamp": "1609459200", "formatted": "Jan 1, 2021" },
      "creationTime": { "timestamp": "1609462800", "formatted": "Jan 1, 2021" },
      "geoDataExif": { "latitude": 51.5074, "longitude": -0.1278, "altitude": 11.0 },
      "geoData": { "latitude": 0.0, "longitude": 0.0, "altitude": 0.0 }
    }
    """;
    var meta = TakeoutJsonParser.Parse(sampleJson, "IMG_1234.jpg");
    Check("photoTakenTime parsed", meta.PhotoTakenTime == DateTimeOffset.FromUnixTimeSeconds(1609459200));
    Check("description parsed", meta.Description == "A nice photo");
    Check("geoDataExif preferred over geoData's null-island placeholder",
        meta.Geo is { } g && Math.Abs(g.Latitude - 51.5074) < 0.0001, meta.Geo?.Latitude.ToString() ?? "null");
    Check("altitude parsed", meta.Geo?.AltitudeMetres == 11.0);
}

{
    // geoDataExif absent, geoData is the only source: falls back to it even though it is
    // Google's own guess rather than the camera's original EXIF.
    const string sampleJson = """
    {
      "photoTakenTime": { "timestamp": "1609459200" },
      "geoData": { "latitude": 40.7128, "longitude": -74.006, "altitude": 10.0 }
    }
    """;
    var meta = TakeoutJsonParser.Parse(sampleJson, "photo.jpg");
    Check("falls back to geoData when geoDataExif is absent",
        meta.Geo is { } g && Math.Abs(g.Latitude - 40.7128) < 0.0001);
}

{
    // Both geo blocks are Google's 0,0 "no location" placeholder: nothing meaningful to write.
    const string sampleJson = """
    {
      "photoTakenTime": { "timestamp": "1609459200" },
      "geoDataExif": { "latitude": 0.0, "longitude": 0.0, "altitude": 0.0 }
    }
    """;
    var meta = TakeoutJsonParser.Parse(sampleJson, "photo.jpg");
    Check("null-island geo is not treated as meaningful", meta.Geo is null || !meta.Geo.IsMeaningful);
}

Check("a sidecar with no time blocks at all parses to a metadata record with null times",
    TakeoutJsonParser.Parse("{}", "photo.jpg").PhotoTakenTime is null);

// ---------------------------------------------------------------- RestoreEngine.BuildExifToolArgs

{
    var meta = new PhotoMetadata(
        "photo.jpg",
        DateTimeOffset.FromUnixTimeSeconds(1609459200),
        null,
        new GeoData(51.5074, -0.1278, 11.0),
        "A caption");
    var exifArgs = RestoreEngine.BuildExifToolArgs(meta);

    Check("date arg written", exifArgs.Any(a => a.StartsWith("-AllDates=2021:01:01")), string.Join(" ", exifArgs));
    Check("latitude written as a positive magnitude with N ref",
        exifArgs.Any(a => a.StartsWith("-GPSLatitude=51.5074")) && exifArgs.Contains("-GPSLatitudeRef=N"));
    Check("negative longitude written as a positive magnitude with W ref",
        exifArgs.Any(a => a.StartsWith("-GPSLongitude=0.1278")) && exifArgs.Contains("-GPSLongitudeRef=W"),
        string.Join(" ", exifArgs));
    Check("altitude written", exifArgs.Any(a => a.StartsWith("-GPSAltitude=11")));
    Check("description written", exifArgs.Any(a => a == "-ImageDescription=A caption"));
}

{
    var meta = new PhotoMetadata("photo.jpg", null, null, null, null);
    var exifArgs = RestoreEngine.BuildExifToolArgs(meta);
    Check("an empty sidecar produces no exiftool args at all", exifArgs.Count == 0, exifArgs.Count.ToString());
}

{
    // Southern and western hemispheres get the opposite reference letters, not just a sign flip
    // that a reader would silently mis-plot on the wrong side of the equator/meridian.
    var meta = new PhotoMetadata("photo.jpg", null, null, new GeoData(-33.8688, 151.2093, null), null);
    var exifArgs = RestoreEngine.BuildExifToolArgs(meta);
    Check("southern latitude gets S ref", exifArgs.Contains("-GPSLatitudeRef=S"));
    Check("positive longitude gets E ref", exifArgs.Contains("-GPSLongitudeRef=E"));
}

// ---------------------------------------------------------------- end to end: a real exiftool write, then read back

// This only runs where exiftool is actually on PATH. It proves the whole pipeline (match, parse,
// copy, write) really lands correct EXIF in a file exiftool itself then reads back, not just
// that the argument list looks right on paper.
var exiftool = ExifToolTools.FindExifTool();

if (exiftool is null)
{
    Skip("end to end restore against a real photo", "exiftool not found on this machine");
}
else
{
    await RunEndToEndCheck(exiftool);
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? $"All checks passed." : $"{failures} check(s) FAILED.");
return failures == 0 ? 0 : 1;

async Task RunEndToEndCheck(string exiftoolPath)
{
    var temp = Path.Combine(Path.GetTempPath(), "metarestoregeek-tests-" + Guid.NewGuid().ToString("N")[..8]);
    var sourceDir = Path.Combine(temp, "source");
    var outputDir = Path.Combine(temp, "output");
    Directory.CreateDirectory(sourceDir);

    try
    {
        // A minimal, valid 4x4 JPEG, so this check needs no binary fixture checked into the
        // repo and needs nothing that can generate one (no ffmpeg/ImageMagick dependency here).
        const string tinyJpegBase64 =
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAEAAQDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDYooor8MP3g//Z";
        var sourcePhoto = Path.Combine(sourceDir, "IMG_TEST.jpg");
        await File.WriteAllBytesAsync(sourcePhoto, Convert.FromBase64String(tinyJpegBase64));

        var sidecarJson = """
        {
          "title": "IMG_TEST.jpg",
          "description": "End to end test photo",
          "photoTakenTime": { "timestamp": "1577880000", "formatted": "Jan 1, 2020" },
          "geoDataExif": { "latitude": 48.8584, "longitude": 2.2945, "altitude": 33.0 }
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "IMG_TEST.jpg.json"), sidecarJson);

        var results = await RestoreEngine.RestoreFolderAsync(exiftoolPath, sourceDir, outputDir);
        Check("one photo processed", results.Count == 1, results.Count.ToString());

        var outcome = results[0];
        Check("restore reported success", outcome.Success, outcome.Message);
        Check("output file was created", outcome.OutputPhotoPath is not null && File.Exists(outcome.OutputPhotoPath));
        Check("original source file is untouched",
            new FileInfo(sourcePhoto).Length == Convert.FromBase64String(tinyJpegBase64).Length);

        if (outcome.OutputPhotoPath is not null && File.Exists(outcome.OutputPhotoPath))
        {
            var (exitCode, stdout) = await ReadBackWithExifTool(exiftoolPath, outcome.OutputPhotoPath);
            Check("exiftool could read the restored file back", exitCode == 0, stdout);
            Check("restored date taken reads back correctly",
                stdout.Contains("2020:01:01"), stdout);
            Check("restored GPS latitude reads back correctly",
                stdout.Contains("48.8584") || stdout.Contains("48 deg 51"), stdout);
        }
    }
    finally
    {
        try { Directory.Delete(temp, recursive: true); } catch { /* best effort */ }
    }
}

async Task<(int ExitCode, string Stdout)> ReadBackWithExifTool(string exiftoolPath, string filePath)
{
    var psi = new System.Diagnostics.ProcessStartInfo(exiftoolPath)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    psi.ArgumentList.Add("-DateTimeOriginal");
    psi.ArgumentList.Add("-GPSLatitude");
    psi.ArgumentList.Add("-GPSLongitude");
    psi.ArgumentList.Add("-ImageDescription");
    psi.ArgumentList.Add(filePath);

    using var process = System.Diagnostics.Process.Start(psi)!;
    var stdout = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    return (process.ExitCode, stdout);
}
