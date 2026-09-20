using System.Text.Json;
using MetaRestoreGeek.Core.Models;

namespace MetaRestoreGeek.Core.Services;

public sealed class TakeoutJsonParseException(string message) : Exception(message);

/// <summary>
/// Parses a single Takeout JSON sidecar into a PhotoMetadata. Pure text in, record out, so this
/// can be tested against real sample sidecars without touching a filesystem or a photo.
/// </summary>
public static class TakeoutJsonParser
{
    public static PhotoMetadata Parse(string json, string originalFileName)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var photoTaken = ReadTimestamp(root, "photoTakenTime");
        var created = ReadTimestamp(root, "creationTime");
        var geo = ReadGeo(root);
        var description = root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
            ? d.GetString()
            : null;

        return new PhotoMetadata(originalFileName, photoTaken, created, geo, string.IsNullOrEmpty(description) ? null : description);
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement root, string propertyName)
    {
        // Every time block in a Takeout sidecar has the same shape: {"timestamp": "unix seconds
        // as a string", "formatted": "human readable, in whatever locale the export ran under"}.
        // The formatted string is not reliable to parse (locale-dependent), so only the
        // timestamp is used.
        if (!root.TryGetProperty(propertyName, out var block) || block.ValueKind != JsonValueKind.Object)
            return null;
        if (!block.TryGetProperty("timestamp", out var ts))
            return null;

        // The timestamp is a JSON string, not a number, in every real export seen.
        var raw = ts.ValueKind == JsonValueKind.String ? ts.GetString() : ts.ToString();
        if (!long.TryParse(raw, out var seconds) || seconds <= 0)
            return null;

        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    private static GeoData? ReadGeo(JsonElement root)
    {
        // Takeout can carry two geo blocks: geoDataExif (what was already in the photo's EXIF
        // when it was taken) and geoData (Google's own best-effort location, sometimes filled
        // in from account/device signals when the photo had none). The EXIF one is the ground
        // truth for what this photo's own camera recorded, so prefer it; fall back to geoData
        // only when the EXIF block is missing or is Google's 0,0 placeholder.
        var fromExif = TryReadGeoBlock(root, "geoDataExif");
        if (fromExif is { IsMeaningful: true })
            return fromExif;

        var fromGeoData = TryReadGeoBlock(root, "geoData");
        if (fromGeoData is { IsMeaningful: true })
            return fromGeoData;

        return fromExif ?? fromGeoData;
    }

    private static GeoData? TryReadGeoBlock(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var block) || block.ValueKind != JsonValueKind.Object)
            return null;
        if (!block.TryGetProperty("latitude", out var lat) || !block.TryGetProperty("longitude", out var lon))
            return null;
        if (lat.ValueKind != JsonValueKind.Number || lon.ValueKind != JsonValueKind.Number)
            return null;

        double? altitude = block.TryGetProperty("altitude", out var alt) && alt.ValueKind == JsonValueKind.Number
            ? alt.GetDouble()
            : null;

        return new GeoData(lat.GetDouble(), lon.GetDouble(), altitude);
    }
}
