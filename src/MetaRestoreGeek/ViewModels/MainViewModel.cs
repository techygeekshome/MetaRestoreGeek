using MetaRestoreGeek.Core.Services;

namespace MetaRestoreGeek.ViewModels;

/// <summary>
/// One screen, one job, same shape as the rest of the range: pick a folder, see what would
/// happen, then do it. The folder is a Takeout "Google Photos" export folder (or any folder that
/// still has the photos sitting next to their .json sidecars, which is how Takeout always
/// delivers them, one folder at a time).
/// </summary>
public sealed class MainViewModel : ObservableObject
{
private static readonly HttpClient _http = new();
private string? _exifToolPath;

public MainViewModel()
{
PickFolderCommand = new AsyncRelayCommand(PickFolderAsync);
StartCommand = new AsyncRelayCommand(StartAsync, () => CanStart);
ShowAboutCommand = new RelayCommand(() => AboutRequested?.Invoke());
DownloadExifToolCommand = new AsyncRelayCommand(DownloadExifToolAsync, () => !IsDownloadingExifTool);
RefreshExifToolState();
}

public event Action? AboutRequested;
public Func<Task<string?>>? RequestOpenFolderDialog;
public Action<string>? RequestRevealInExplorer;

public bool ExifToolAvailable => _exifToolPath is not null;
public string ExifToolMissingMessage =>
$"MetaRestoreGeek needs exiftool to write dates and locations back into your photos and does not bundle a copy. Fetch it below: a one-time download of about {ExifToolCatalog.ApproxBytes / 1_000_000d:0} MB from {ExifToolCatalog.PublisherUrl}, checked against a known checksum before anything runs. Already have exiftool on your PATH instead? It will be picked up automatically, no need to download again.";

public string ExifToolDownloadButtonText => "Download exiftool";

private bool _isDownloadingExifTool;
public bool IsDownloadingExifTool
{
get => _isDownloadingExifTool;
private set
{
if (!SetField(ref _isDownloadingExifTool, value)) return;
((AsyncRelayCommand)DownloadExifToolCommand).RaiseCanExecuteChanged();
}
}

private double _exifToolDownloadProgress;
public double ExifToolDownloadProgress { get => _exifToolDownloadProgress; private set => SetField(ref _exifToolDownloadProgress, value); }

private string _exifToolDownloadStatus = "";
public string ExifToolDownloadStatus { get => _exifToolDownloadStatus; private set => SetField(ref _exifToolDownloadStatus, value); }

public System.Windows.Input.ICommand DownloadExifToolCommand { get; }

private void RefreshExifToolState()
{
_exifToolPath = ExifToolTools.FindExifTool();
OnPropertyChanged(nameof(ExifToolAvailable));
((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();
}

private async Task DownloadExifToolAsync()
{
IsDownloadingExifTool = true;
ExifToolDownloadStatus = "Downloading exiftool...";
ExifToolDownloadProgress = 0;
try
{
var progress = new Progress<double>(p =>
{
ExifToolDownloadProgress = p;
ExifToolDownloadStatus = p < 1.0 ? $"Downloading exiftool... {p:P0}" : "Verifying...";
});
await ExifToolCatalog.DownloadAsync(_http, progress);
ExifToolDownloadStatus = "";
RefreshExifToolState();
}
catch (ExifToolDownloadException ex)
{
ExifToolDownloadStatus = ex.Message;
}
catch (Exception ex)
{
ExifToolDownloadStatus = $"Could not download exiftool: {ex.Message}";
}
finally
{
IsDownloadingExifTool = false;
}
}

private string? _sourceFolder;
public string? SourceFolder
{
get => _sourceFolder;
private set
{
if (!SetField(ref _sourceFolder, value)) return;
OnPropertyChanged(nameof(SourceFolderName));
OnPropertyChanged(nameof(HasSource));
}
}

public string SourceFolderName => SourceFolder is null ? "" : Path.GetFileName(SourceFolder.TrimEnd(Path.DirectorySeparatorChar));
public bool HasSource => SourceFolder is not null;

private int _photoCount;
public int PhotoCount { get => _photoCount; private set => SetField(ref _photoCount, value); }

private int _matchedCount;
public int MatchedCount { get => _matchedCount; private set => SetField(ref _matchedCount, value); }

public string PreviewSummary => !HasSource ? "" :
PhotoCount == 0 ? "No photos or videos found in that folder." :
$"{MatchedCount} of {PhotoCount} have a matching Takeout sidecar and can be restored.";

private bool _isBusy;
public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

private double _progressFraction;
public double ProgressFraction { get => _progressFraction; private set => SetField(ref _progressFraction, value); }

private string _statusText = "";
public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }

private string? _outputFolder;
public string? OutputFolder
{
get => _outputFolder;
private set { SetField(ref _outputFolder, value); OnPropertyChanged(nameof(HasResult)); }
}
public bool HasResult => OutputFolder is not null;

private bool CanStart => HasSource && PhotoCount > 0 && !IsBusy && ExifToolAvailable;

public System.Windows.Input.ICommand PickFolderCommand { get; }
public System.Windows.Input.ICommand StartCommand { get; }
public System.Windows.Input.ICommand ShowAboutCommand { get; }

public void LoadFolder(string path)
{
SourceFolder = path;
OutputFolder = null;
StatusText = "";

// Counting matches here is deliberately just SidecarMatcher against a directory listing,
// no JSON parsing and no exiftool: it needs to be fast enough to run the instant a folder
// is dropped, so the person sees whether this folder is even worth restoring before
// committing to anything.
var mediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{ ".jpg", ".jpeg", ".png", ".heic", ".gif", ".webp", ".mp4", ".mov", ".avi", ".mkv" };

var allNames = Directory.GetFiles(path).Select(Path.GetFileName).Cast<string>().ToList();
var photoNames = allNames.Where(n => mediaExtensions.Contains(Path.GetExtension(n))).ToList();

PhotoCount = photoNames.Count;
MatchedCount = photoNames.Count(n => SidecarMatcher.Match(n, allNames).Found);
((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();
OnPropertyChanged(nameof(PreviewSummary));
}

private async Task PickFolderAsync()
{
if (RequestOpenFolderDialog is null) return;
var path = await RequestOpenFolderDialog();
if (path is not null)
LoadFolder(path);
}

private async Task StartAsync()
{
if (SourceFolder is null || _exifToolPath is null) return;

IsBusy = true;
OutputFolder = null;
ProgressFraction = 0;
StatusText = "Starting...";

try
{
var outputPath = NextFreeOutputFolder(SourceFolder);
var progress = new Progress<RestoreProgress>(p =>
{
ProgressFraction = p.Total == 0 ? 0 : (double)p.Done / p.Total;
StatusText = p.Done < p.Total ? $"Restoring {p.CurrentFile}..." : "Finishing up...";
});

var results = await RestoreEngine.RestoreFolderAsync(_exifToolPath, SourceFolder, outputPath, progress);

var restored = results.Count(r => r.Success);
var skipped = results.Count - restored;
OutputFolder = outputPath;
StatusText = skipped == 0
? $"Restored {restored} of {results.Count} photos."
: $"Restored {restored} of {results.Count} photos. {skipped} had no sidecar or failed; nothing was changed for those.";
}
catch (Exception ex)
{
StatusText = $"That did not work: {ex.Message}";
}
finally
{
IsBusy = false;
}
}

public void RevealResult()
{
if (OutputFolder is not null)
RequestRevealInExplorer?.Invoke(OutputFolder);
}

private static string NextFreeOutputFolder(string sourceFolder)
{
var parent = Path.GetDirectoryName(sourceFolder.TrimEnd(Path.DirectorySeparatorChar)) ?? ".";
var stem = Path.GetFileName(sourceFolder.TrimEnd(Path.DirectorySeparatorChar));
var candidate = Path.Combine(parent, $"{stem} (restored)");
var n = 2;
while (Directory.Exists(candidate) && Directory.EnumerateFileSystemEntries(candidate).Any())
{
candidate = Path.Combine(parent, $"{stem} (restored {n})");
n++;
}
return candidate;
}
}
