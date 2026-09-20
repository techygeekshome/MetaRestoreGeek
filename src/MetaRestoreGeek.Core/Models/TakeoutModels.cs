namespace MetaRestoreGeek.Core.Models;

/// <summary>GPS coordinates from a Takeout sidecar. Google writes 0,0 for "no location", which
/// is a real place in the Gulf of Guinea and not something worth writing into a photo.</summary>
public sealed record GeoData(double Latitude, double Longitude, double? AltitudeMetres)
{
    public bool IsMeaningful => Math.Abs(Latitude) > 0.0001 || Math.Abs(Longitude) > 0.0001;
}

/// <summary>What a Takeout JSON sidecar actually gives us, once parsed.</summary>
public sealed record PhotoMetadata(
    string OriginalFileName,
    DateTimeOffset? PhotoTakenTime,
    DateTimeOffset? CreationTime,
    GeoData? Geo,
    string? Description);

/// <summary>How a sidecar was found for a photo. Kept on the result so a low-confidence match
/// can be shown to the user before anything is written, not silently trusted.</summary>
public enum MatchStrategy
{
    Exact,              // photo.jpg -> photo.jpg.json
    SupplementalSuffix, // photo.jpg -> photo.jpg.supplemental-metadata.json
    EditedSuffixStripped, // photo-edited.jpg -> photo.jpg.json
    NumberedDuplicate,  // photo(1).jpg -> photo.jpg(1).json
    TruncatedPrefix,    // long-filename-that-got-cut.jpg -> long-filename-that-got-c.json (best-effort)
    None
}

public sealed record SidecarMatch(string PhotoFileName, string? SidecarFileName, MatchStrategy Strategy)
{
    public bool Found => SidecarFileName is not null;
}

public sealed record RestoreOutcome(
    string SourcePhotoPath,
    string? OutputPhotoPath,
    bool Success,
    string Message);
