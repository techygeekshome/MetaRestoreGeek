using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace MetaRestoreGeek.Core.Services;

/// <summary>
/// Fetches exiftool on demand, the same technique VideoSizeGeek now uses for ffmpeg: without
/// exiftool MetaRestoreGeek cannot write a single tag back into a photo, so telling a new user to
/// go and find it themselves before the app can do its one job is not good enough. This is that
/// fix, applied here too.
///
/// Why it is downloaded rather than bundled:
///
/// 1. Update responsibility. exiftool's own maintainer ships new tag definitions constantly as
/// camera makers change their formats; vendoring a copy inside our installer means every
/// MetaRestoreGeek release would need to also track exiftool releases just to stay current.
/// Fetching it fresh from its own publisher, at the user's request, does not.
/// 2. Size. The Windows package is around 13 MB compressed and unpacks to roughly 50 MB (it
/// carries a stripped portable Perl runtime), several times the installer itself.
///
/// The archive is pinned to an exact version and checked against a known SHA-256 before anything
/// is extracted. A moving "latest" URL cannot be verified, so it is not used. Downloaded from
/// oliverbetz.de, the same Windows package exiftool.org's own site links to and builds its
/// official Windows distribution from: exiftool.org says outright "exiftool.org also uses my
/// launcher and Perl system for the Windows version of ExifTool", so this is the publisher's own
/// recommended distribution for Windows, not a third-party rebuild.
/// </summary>
public static class ExifToolCatalog
{
  public const string DownloadUrl =
    "https://oliverbetz.de/cms/files/Artikel/ExifTool-for-Windows/exiftool-13.59_64.zip";

  public const string ExpectedSha256 =
    "577257dd22baebe77157d905792d6ed2c5916cd03aa627f40b2175db12110ac6";

  public const long ApproxBytes = 13_533_363;

  public const string Version = "13.59";

  public const string PublisherUrl = "https://exiftool.org/";

  public static string ToolsDirectory => ExifToolTools.CacheDirectory;

  private static string ExeName() => OperatingSystem.IsWindows() ? "exiftool.exe" : "exiftool";

  public static string ExifToolPath => Path.Combine(ToolsDirectory, ExeName());

  public static bool IsDownloaded =>
    File.Exists(ExifToolPath) &&
    Directory.Exists(Path.Combine(ToolsDirectory, "exiftool_files"));

  public static async Task DownloadAsync(
    HttpClient http,
    IProgress<double>? progress = null,
    CancellationToken ct = default)
  {
    Directory.CreateDirectory(ToolsDirectory);
    var archive = Path.Combine(ToolsDirectory, "exiftool.zip.part");
    var scratch = Path.Combine(ToolsDirectory, "unpack.tmp");

    TryDelete(archive);
    TryDeleteDirectory(scratch);

    try
    {
      using (var response = await http
             .GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
             .ConfigureAwait(false))
      {
        response.EnsureSuccessStatusCode();
        var expected = response.Content.Headers.ContentLength ?? ApproxBytes;

        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dest = File.Create(archive);

        var buffer = new byte[1 << 20];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
          await dest.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
          total += read;
          progress?.Report(Math.Min(1.0, (double)total / expected));
        }
      }

      var actual = await Sha256Async(archive, ct).ConfigureAwait(false);
      if (!string.Equals(actual, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
      {
        throw new ExifToolDownloadException(
          "The downloaded file did not match the expected checksum, so it was discarded. " +
          "Nothing was installed. This usually means the download was interrupted or " +
          "something on the network altered it.");
      }

      Directory.CreateDirectory(scratch);
      ZipFile.ExtractToDirectory(archive, scratch);

      // The package is flat: the launcher exe and its exiftool_files runtime folder sit
      // side by side at the archive root, not nested under a versioned subfolder the way
      // ffmpeg's archive is. Both have to land in the same folder, or the launcher cannot
      // find its own bundled Perl runtime.
      var launcher = FindLauncher(scratch)
        ?? throw new ExifToolDownloadException("The archive did not contain ExifTool.exe.");
      var filesDir = Path.Combine(Path.GetDirectoryName(launcher)!, "exiftool_files");
      if (!Directory.Exists(filesDir))
        throw new ExifToolDownloadException("The archive did not contain the exiftool_files folder.");

      File.Copy(launcher, ExifToolPath, overwrite: true);
      CopyDirectory(filesDir, Path.Combine(ToolsDirectory, "exiftool_files"));

      progress?.Report(1.0);
    }
    finally
    {
      TryDelete(archive);
      TryDeleteDirectory(scratch);
    }
  }

  public static void Delete()
  {
    TryDelete(ExifToolPath);
    TryDeleteDirectory(Path.Combine(ToolsDirectory, "exiftool_files"));
  }

  private static string? FindLauncher(string root) =>
    Directory.EnumerateFiles(root, "ExifTool.exe", SearchOption.AllDirectories).FirstOrDefault();

  private static void CopyDirectory(string sourceDir, string destDir)
  {
    Directory.CreateDirectory(destDir);
    foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
    {
      var relative = Path.GetRelativePath(sourceDir, file);
      var target = Path.Combine(destDir, relative);
      Directory.CreateDirectory(Path.GetDirectoryName(target)!);
      File.Copy(file, target, overwrite: true);
    }
  }

  private static async Task<string> Sha256Async(string path, CancellationToken ct)
  {
    await using var fs = File.OpenRead(path);
    var hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
    return Convert.ToHexString(hash).ToLowerInvariant();
  }

  private static void TryDelete(string path)
  {
    try { if (File.Exists(path)) File.Delete(path); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }

  private static void TryDeleteDirectory(string path)
  {
    try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }
}

public sealed class ExifToolDownloadException : Exception
{
public ExifToolDownloadException(string message) : base(message) { }
}
