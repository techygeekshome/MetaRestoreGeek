<div align="center">

<img src="https://raw.githubusercontent.com/techygeekshome/MetaRestoreGeek/main/icons/metarestoregeek-256.png" alt="MetaRestoreGeek logo" width="96" height="96">

# MetaRestoreGeek

**Puts the dates and GPS locations from a Google Takeout export back into the photos themselves.**

[![Version](https://img.shields.io/github/v/release/techygeekshome/MetaRestoreGeek?label=version&color=4c9bff)](https://github.com/techygeekshome/MetaRestoreGeek/releases)
[![Platform](https://img.shields.io/badge/platform-Windows-0078d4)](#%EF%B8%8F-download--run)
[![License](https://img.shields.io/badge/license-GPL--3.0-3fb950)](LICENSE)
[![Made by TechyGeeksHome](https://img.shields.io/badge/made%20by-TechyGeeksHome-b191f2)](https://techygeekshome.info)
[![Support on Ko-fi](https://img.shields.io/badge/support-Ko--fi-ff5e5b)](https://ko-fi.com/techygeekshome)

[Download](#%EF%B8%8F-download--run) · [Features](#-what-it-does) · [Requirements](#-what-your-pc-needs) · [Build from source](#-build-from-source) · [License](#-license)

</div>

---

<!--VIDEO-->
Google Takeout exports every photo's date taken and GPS location into a separate `.json` file next to it, not into the photo itself. Import that export into anything else (Windows Photos, a NAS, another cloud service) and every photo looks like it was taken today, from nowhere. MetaRestoreGeek matches each photo back to its sidecar, including the naming quirks Google's own export creates, and writes the real date and location back in, to a new copy, never the original.

## 📸 Screenshots

<p float="left">
  <img src="https://raw.githubusercontent.com/techygeekshome/MetaRestoreGeek/main/screenshots/screenshot-start.png" width="49%" />
  <img src="https://raw.githubusercontent.com/techygeekshome/MetaRestoreGeek/main/screenshots/screenshot-folder.png" width="49%" />
</p>
<p float="left">
  <img src="https://raw.githubusercontent.com/techygeekshome/MetaRestoreGeek/main/screenshots/screenshot-restoring.png" width="49%" />
  <img src="https://raw.githubusercontent.com/techygeekshome/MetaRestoreGeek/main/screenshots/screenshot-done.png" width="49%" />
</p>

## ⬇️ Download & run

| What it is | Get it |
| --- | --- |
| **Installer** *(recommended)*: Start menu entry, proper uninstall, no admin rights needed | [**MetaRestoreGeekSetup.exe**](https://github.com/techygeekshome/MetaRestoreGeek/releases/latest) |
| **Portable**: one `.exe`, nothing to install | [**MetaRestoreGeek-portable.exe**](https://github.com/techygeekshome/MetaRestoreGeek/releases/latest) |

Both files are checked against `SHA256SUMS.txt` on every release. You can also read about it on the [product page](https://techygeekshome.info/metarestoregeek/).

> **Windows will warn you the first time.** MetaRestoreGeek isn't code signed yet, so SmartScreen shows a warning on first run. Click **More info → Run anyway**. [Why that happens, and how to check the download is genuine.](https://techygeekshome.info/why-windows-warns-about-this-download/)

## ✨ What it does

- 📂 **Point it at a Takeout "Google Photos" folder** and it works out how many of the photos in it have a matching sidecar before you commit to anything.
- 🧩 **Handles the real naming quirks**, not just the easy `photo.jpg` + `photo.jpg.json` case: the newer `.supplemental-metadata.json` suffix, Google Photos' own "edited" copies (which share the original's sidecar), numbered duplicates (where the `(1)` counter moves to *after* the file extension on the sidecar), and truncated long filenames from older exports.
- 📍 **Writes date taken and GPS location** (preferring the camera's own original EXIF location over Google's best-effort guess, when the export has both), plus the caption if there is one.
- 🛡️ **Never touches the original.** Every restored photo is a new file in a `(restored)` folder next to the source; nothing in the source folder is ever opened for writing.
- 📋 **Tells you what happened to each photo**, including the ones with no matching sidecar, rather than silently skipping them.

## 💻 What your PC needs

- Windows 10 or 11, 64-bit.
- **exiftool**, fetched with one click the first time you need it. MetaRestoreGeek does not bundle it (it updates constantly, so a bundled copy would go stale fast), but the first time it can't find a copy it offers a one-click download of a pinned, hash-verified exiftool build from [oliverbetz.de](https://oliverbetz.de/cms/files/Artikel/ExifTool-for-Windows/), the same Windows package exiftool.org's own site points to. Already have exiftool on your PATH? It's picked up automatically and nothing is downloaded.

## 🤔 Why this exists

Anyone who has moved photos out of Google Photos via Takeout hits the same wall: the export is correct, complete, and useless the moment you drag it anywhere else, because every date and location lives in a `.json` file the destination has no idea to look for. The naming scheme those sidecars use has enough real edge cases (edited copies, reordered duplicate counters, truncated filenames) that doing this properly is most of the actual engineering, which is exactly the part MetaRestoreGeek gets right.

## 🔧 Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build MetaRestoreGeek.sln -c Release
dotnet run --project tests/MetaRestoreGeek.Tests -c Release
```

The tests include a real end-to-end restore against a generated photo, written with exiftool and read back to confirm it landed, so that part needs exiftool on PATH. Everything else runs anywhere .NET 8 does.

## ☕ Support

MetaRestoreGeek is free and always will be. If it saved you an afternoon, you can [buy us a coffee on Ko-fi](https://ko-fi.com/techygeekshome). Welcome, but never expected.

## 🐛 Support & contributing

Found a bug or have a request? [Open an issue](https://github.com/techygeekshome/MetaRestoreGeek/issues) or [get in touch](https://techygeekshome.info/contact/). Pull requests are welcome.

## 📄 License

MetaRestoreGeek is free software, licensed under the [GNU General Public License v3.0](LICENSE). The MetaRestoreGeek name, logo and TechyGeeksHome branding are not covered by that licence and remain ours.

© 2026 TechyGeeksHome. The rest of the range is at [techygeekshome.info/geek-tools](https://techygeekshome.info/geek-tools/).

---

<div align="center">

Made with ❤️ by [**TechyGeeksHome**](https://techygeekshome.info)

[Website](https://techygeekshome.info) · [YouTube](https://www.youtube.com/channel/UCtEuFj1SMLiuRoucD1hv8dA) · [X](https://x.com/TechyGeeks1) · [Instagram](https://www.instagram.com/andrewarmstrongtgh/)

</div>
