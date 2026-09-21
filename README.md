<div align="center">

<img src="icons/metarestoregeek-256.png" alt="MetaRestoreGeek logo" width="96" height="96">

# MetaRestoreGeek

**Puts the dates and GPS locations from a Google Takeout export back into the photos themselves.**

[![Build](https://github.com/techygeekshome/MetaRestoreGeek/actions/workflows/build.yml/badge.svg)](https://github.com/techygeekshome/MetaRestoreGeek/actions/workflows/build.yml)
[![Version](https://img.shields.io/github/v/release/techygeekshome/MetaRestoreGeek?label=version&color=4c9bff)](https://github.com/techygeekshome/MetaRestoreGeek/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078d4)](#download)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue)](LICENSE)
[![Made by TechyGeeksHome](https://img.shields.io/badge/made%20by-TechyGeeksHome-b191f2)](https://techygeekshome.info)
[![Support on Ko-fi](https://img.shields.io/badge/support-Ko--fi-ff5e5b)](https://ko-fi.com/techygeekshome)

[Download](#download) · [What it does](#what-it-does) · [Requirements](#requirements) · [Why this exists](#why-this-exists)

</div>

---

Google Takeout exports every photo's date taken and GPS location into a separate `.json` file
next to it, not into the photo itself. Import that export into anything else, Windows Photos,
a NAS, another cloud service, and every photo looks like it was taken today, from nowhere.
MetaRestoreGeek matches each photo back to its sidecar, including the naming quirks Google's own
export creates, and writes the real date and location back in, to a new copy, never the original.

Part of the [TechyGeeksHome](https://techygeekshome.info/geek-tools/) range.

---

## Download

**[Download the latest release](https://github.com/techygeekshome/MetaRestoreGeek/releases/latest)**, or read about it on the
[product page](https://techygeekshome.info/metarestoregeek/).

Two files on every release, both checked against `SHA256SUMS.txt`:

- **MetaRestoreGeekSetup.exe**: installer, no admin rights needed, installs for your account only.
- **MetaRestoreGeek-portable.exe**: no installer, just run it.

MetaRestoreGeek is not code signed yet, so Windows SmartScreen will warn on first run. [Why that
happens, and how to check the download is genuine.](https://techygeekshome.info/why-windows-warns-about-this-download/)

## What it does

- **Point it at a Takeout "Google Photos" folder** and it works out how many of the photos in it
  have a matching sidecar before you commit to anything.
- **Handles the real naming quirks**, not just the easy `photo.jpg` + `photo.jpg.json` case:
  the newer `.supplemental-metadata.json` suffix, Google Photos' own "edited" copies (which
  share the original's sidecar), numbered duplicates (where the `(1)` counter moves to *after*
  the file extension on the sidecar), and truncated long filenames from older exports.
- **Writes date taken and GPS location** (preferring the camera's own original EXIF location
  over Google's best-effort guess, when the export has both), plus the caption if there is one.
- **Never touches the original.** Every restored photo is a new file in a `(restored)` folder
  next to the source; nothing in the source folder is ever opened for writing.
- **Tells you what happened to each photo**, including the ones with no matching sidecar, rather
  than silently skipping them.

## Requirements

- Windows 10 or 11, 64-bit.
- **exiftool**, fetched with one click the first time you need it. MetaRestoreGeek does not bundle it (it ships its own tag definitions and updates constantly, so bundling a stale copy would go wrong fast), but the first time it can't find a copy, it offers a one-click, in-app download of a pinned, hash-verified exiftool build from [oliverbetz.de](https://oliverbetz.de/cms/files/Artikel/ExifTool-for-Windows/), the same Windows package exiftool.org's own site points to, checked against a known checksum before anything runs. Already have exiftool on your PATH instead? It's picked up automatically and nothing is downloaded.

## Why this exists

Anyone who has moved photos out of Google Photos via Takeout hits the same wall: the export is
correct, complete, and functionally useless the moment you drag it anywhere else, because every
date and location lives in a `.json` file the destination has no idea to look for. The naming
scheme those sidecars use is not a straightforward pairing either; it has enough real edge cases
(edited copies, reordered duplicate counters, truncated filenames) that a tool doing this properly
is most of the actual engineering, which is exactly the part MetaRestoreGeek gets right.

## Building it yourself

```
dotnet build MetaRestoreGeek.sln -c Release
dotnet run --project tests/MetaRestoreGeek.Tests -c Release
```

The test project includes a real end-to-end restore against a generated test photo, written with
exiftool and then read back with exiftool to confirm it landed, so it needs exiftool on PATH to
run that part; everything else runs anywhere .NET 8 does.

## License

GPL-3.0. See [LICENSE](LICENSE).

## More from TechyGeeksHome

The rest of the range is at [techygeekshome.info/geek-tools](https://techygeekshome.info/geek-tools/).
