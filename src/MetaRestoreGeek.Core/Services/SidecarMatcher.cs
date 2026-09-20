using MetaRestoreGeek.Core.Models;

namespace MetaRestoreGeek.Core.Services;

/// <summary>
/// Finds a photo's JSON sidecar inside a Takeout export. This is the part worth getting right:
/// Google's own filenames are not a straightforward "photo.jpg + photo.jpg.json" pair, and a
/// tool that only handles the easy case will silently skip a large fraction of a real export.
/// No file I/O here on purpose, so every rule can be proven against a plain list of names.
/// </summary>
public static class SidecarMatcher
{
    private const string SupplementalSuffix = ".supplemental-metadata.json";

    public static SidecarMatch Match(string photoFileName, IReadOnlyCollection<string> filesInFolder)
    {
        // A case-insensitive lookup, because the export was almost certainly unzipped on
        // Windows, where the filesystem does not distinguish "IMG_1234.JPG" from "img_1234.jpg".
        var set = new HashSet<string>(filesInFolder, StringComparer.OrdinalIgnoreCase);

        // 1. The plain case: photo.jpg -> photo.jpg.json
        var exact = photoFileName + ".json";
        if (set.Contains(exact))
            return new SidecarMatch(photoFileName, exact, MatchStrategy.Exact);

        // 2. Later Takeout exports append this longer suffix instead of a bare .json.
        var supplemental = photoFileName + SupplementalSuffix;
        if (set.Contains(supplemental))
            return new SidecarMatch(photoFileName, supplemental, MatchStrategy.SupplementalSuffix);

        // 3. Google Photos' own "edited" copy (IMG_1234-edited.jpg) has no sidecar of its own;
        // it shares the original's. Strip the suffix and look again for that base name's json.
        var stem = Path.GetFileNameWithoutExtension(photoFileName);
        var ext = Path.GetExtension(photoFileName);
        const string editedSuffix = "-edited";
        if (stem.EndsWith(editedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var originalName = stem[..^editedSuffix.Length] + ext;
            var originalJson = originalName + ".json";
            if (set.Contains(originalJson))
                return new SidecarMatch(photoFileName, originalJson, MatchStrategy.EditedSuffixStripped);
            var originalSupplemental = originalName + SupplementalSuffix;
            if (set.Contains(originalSupplemental))
                return new SidecarMatch(photoFileName, originalSupplemental, MatchStrategy.EditedSuffixStripped);
        }

        // 4. Numbered duplicates. Takeout names the second copy of a photo "IMG_1234(1).jpg" but
        // (this is the part that catches most naive matchers out) the counter on its sidecar
        // moves to AFTER the extension: "IMG_1234.jpg(1).json", not "IMG_1234(1).jpg.json".
        var dupMatch = TryExtractTrailingCounter(stem);
        if (dupMatch is { } counter)
        {
            var baseName = stem[..^counter.RawText.Length] + ext;
            var reorderedJson = $"{baseName}({counter.Number}).json";
            if (set.Contains(reorderedJson))
                return new SidecarMatch(photoFileName, reorderedJson, MatchStrategy.NumberedDuplicate);
        }

        // 5. Long filenames: older exports truncate the sidecar's base name to fit a length
        // limit, so the json on disk can be a strict prefix of the photo's own name rather than
        // a full match. Only trust this when exactly one candidate fits, since a short, generic
        // prefix could otherwise match several unrelated files.
        var prefixCandidates = set
            .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Where(f => IsPlausibleTruncatedSidecar(photoFileName, f))
            .ToList();
        if (prefixCandidates.Count == 1)
            return new SidecarMatch(photoFileName, prefixCandidates[0], MatchStrategy.TruncatedPrefix);

        return new SidecarMatch(photoFileName, null, MatchStrategy.None);
    }

    private readonly record struct TrailingCounter(string RawText, int Number);

    /// <summary>Pulls a trailing "(N)" off a filename stem, e.g. "IMG_1234(1)" -> ("(1)", 1).</summary>
    private static TrailingCounter? TryExtractTrailingCounter(string stem)
    {
        if (!stem.EndsWith(')')) return null;
        var openParen = stem.LastIndexOf('(');
        if (openParen < 0) return null;
        var inner = stem[(openParen + 1)..^1];
        return int.TryParse(inner, out var n) ? new TrailingCounter(stem[openParen..], n) : null;
    }

    /// <summary>
    /// True when `sidecarName` looks like a length-truncated version of `photoFileName`'s own
    /// json sidecar: its name (with the .json and any supplemental suffix removed) is a prefix
    /// of the photo's full filename, is at least 8 characters (long enough not to be a coincidence
    /// on a short name), and is shorter than the photo's own name (otherwise it would already
    /// have matched one of the exact strategies above).
    /// </summary>
    private static bool IsPlausibleTruncatedSidecar(string photoFileName, string sidecarName)
    {
        var candidateBase = sidecarName.EndsWith(SupplementalSuffix, StringComparison.OrdinalIgnoreCase)
            ? sidecarName[..^SupplementalSuffix.Length]
            : sidecarName[..^".json".Length];

        return candidateBase.Length >= 8
            && candidateBase.Length < photoFileName.Length
            && photoFileName.StartsWith(candidateBase, StringComparison.OrdinalIgnoreCase);
    }
}
