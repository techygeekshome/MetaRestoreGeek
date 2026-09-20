using TechyGeeksHome.Common;

namespace MetaRestoreGeek;

/// <summary>
/// Everything the shared About window and update check need to know about this app. One place,
/// so the wording here and the wording on the product page can be kept in step.
/// </summary>
internal static class AppFacts
{
    public static readonly AppInfo Info = new()
    {
        Name = "MetaRestoreGeek",
        Tagline = "Puts the dates and locations back into your Google Takeout photos",
        Description =
            "Google Takeout exports every photo's date taken and GPS location into a separate " +
            ".json file next to it, not into the photo itself, so the moment you import that " +
            "export anywhere else, every photo looks like it was taken today with no location. " +
            "MetaRestoreGeek matches each photo to its sidecar, including the tricky cases Google's " +
            "own naming creates (edited copies, numbered duplicates, truncated filenames), and " +
            "writes the real date and GPS back in, always to a new copy next to the original. " +
            "Runs on your own machine, on exiftool, which you install once yourself.",
        GitHubOwner = "techygeekshome",
        GitHubRepo = "MetaRestoreGeek",
        ProductUrl = "https://techygeekshome.info/metarestoregeek/",
        IconUri = "avares://MetaRestoreGeek/Assets/metarestoregeek.png",
        LicenceLine = "Free to use, including at work. GPL-3.0. No paid tier, ever.",
        Credits = new[]
        {
            new Credit("exiftool", "Perl Artistic License", "https://exiftool.org"),
            new Credit("Avalonia", "MIT", "https://avaloniaui.net")
        }
    };
}
