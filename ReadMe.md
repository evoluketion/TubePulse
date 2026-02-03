# TubePulse

[![Release version](https://img.shields.io/github/v/release/lukemcilia/TubePulse?color=brightgreen&label=Download&style=for-the-badge)](#installation "Installation")
[![Commits](https://img.shields.io/github/commit-activity/m/yt-dlp/yt-dlp?label=commits&style=for-the-badge)](https://github.com/lukemcilia/TubePulse/commits "Commit History")

TubePulse is a console app that runs as a hosted background service and **polls one or more YouTube channels** for newly uploaded videos. It uses **`yt-dlp`** to fetch a channel’s recent uploads, keeps a **per-channel cache** of already-seen video IDs, and downloads any newly detected videos into a per-channel folder.
<img width="1299" height="598" alt="image" src="https://github.com/user-attachments/assets/64a56129-0966-41d1-a42c-be0a3e2339cb" />


## Features

- **Multi-channel polling** via `appsettings.json`
- **Per-channel download resolution override** (falls back to a global default)
- **Per-channel cache files** to avoid re-downloading the same videos
- **Skips Shorts** when listing videos (filters out `/shorts/`)

## Requirements

- **.NET SDK 8.0**
- **`yt-dlp`** is automatically downloaded and updated on startup (no manual installation required)

## Installation
[![Windows](https://img.shields.io/badge/-Windows_x64-blue.svg?style=for-the-badge&logo=windows)](https://github.com/lukemcilia/TubePulse/releases/latest/download/TubePulse-windows-latest.zip)
[![Unix](https://img.shields.io/badge/-Linux/BSD-red.svg?style=for-the-badge&logo=linux)](https://github.com/lukemcilia/TubePulse/releases/latest/download/TubePulse-ubuntu-latest.zip)

## Configuration

TubePulse reads settings from `appsettings.json` under the `TubePulse` section.

### Example `appsettings.json`

```json
{
  "TubePulse": {
    "Channels": [
      {
        "Name": "Google",
        "Url": "https://www.youtube.com/@Google",
        "DownloadResolution": "1080",
        "DownloadCodec": "vp9"
      }
    ],
    "DownloadPath": "C:/Users/<you>/Videos/TubePulse",
    "DownloadCodec": "h264",
    "CachePath": "C:/Users/<you>/Videos/TubePulse/Cache",
    "PollingTimeoutHours": 1,
    "DownloadResolution": "720"
  }
}
```

### Settings reference

- **`TubePulse:Channels`**
  - **`Name`**: Used for the per-channel download folder name and cache file name.
  - **`Url`**: Channel URL (e.g. `https://www.youtube.com/@SomeChannel`).
  - **`DownloadResolution`**: Optional override (string number like `720`, `1080`, `2160`).
  - **`DownloadCodec`**: Optional override (e.g. `av01`, `vp9`, `h265`, `h264`). See [yt-dlp formats](https://github.com/yt-dlp/yt-dlp?tab=readme-ov-file#sorting-formats) for usable examples.
- **`TubePulse:DownloadPath`**: Root directory where videos are downloaded.
  - Downloads land in: `DownloadPath/<ChannelName>/`
- **`TubePulse:CachePath`**: Directory where cache JSON files are stored.
  - Cache filename pattern: `videoCache_<ChannelName>.json`
- **`TubePulse:PollingTimeoutHours`**: How long to wait between checks.
- **`TubePulse:DownloadResolution`**: Default resolution used when a channel doesn’t specify one.
- **`TubePulse:DownloadCodec`**: Default codec used when a channel doesn’t specify one (e.g. `av01`, `vp9`, `h265`, `h264`). See [yt-dlp formats](https://github.com/yt-dlp/yt-dlp?tab=readme-ov-file#sorting-formats) for usable examples.

## Running

### Debug and run locally

```bash
dotnet run
```

### Release build

```bash
dotnet build --configuration Release --output ./bin/Release
```

## How it works

- On startup, TubePulse loads a cache of processed video IDs for each channel.
- **First run behavior**: if there is no cache for a channel, TubePulse fetches *all* existing video IDs for that channel and caches them (so it won’t download the entire back-catalog).
- On each polling cycle, TubePulse asks `yt-dlp` for recent uploads (last day) and downloads any video IDs it hasn’t seen before.

## Troubleshooting

- **App prints: `DownloadPath and CachePath must be specified`**
  - Set `TubePulse:DownloadPath` and `TubePulse:CachePath` in `appsettings.json`.
- **Nothing downloads on first run**
  - This is expected: the first run only caches existing videos so it starts downloading on *new* uploads going forward.
- **Downloads failing for unknown reasons**
  - TubePulse automatically updates yt-dlp on startup, but if issues persist, try deleting the yt-dlp binary from `~/.local/share/TubePulse/` (Linux) or `%LOCALAPPDATA%\TubePulse\` (Windows) to force a fresh download.

## Notes

- TubePulse filters out YouTube Shorts when listing videos.
- Downloads are performed by `yt-dlp`; the exact format selection string currently targets a max height equal to the configured resolution.
