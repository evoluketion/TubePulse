# TubePulse

[![Release version](https://img.shields.io/github/v/release/lukemcilia/TubePulse?color=brightgreen&label=Download&style=for-the-badge)](#installation "Installation")
[![Commits](https://img.shields.io/github/commit-activity/m/yt-dlp/yt-dlp?label=commits&style=for-the-badge)](https://github.com/lukemcilia/TubePulse/commits "Commit History")

TubePulse is a console app that runs as a hosted background service and **polls one or more YouTube channels** for newly uploaded videos. It uses **`yt-dlp`** to fetch a channel’s recent uploads, keeps a **per-channel cache** of already-seen video IDs, and downloads any newly detected videos into a per-channel folder.

## Features

- **Multi-channel polling** via `appsettings.json`
- **Per-channel download resolution override** (falls back to a global default)
- **Per-channel cache files** to avoid re-downloading the same videos
- **Skips Shorts** when listing videos (filters out `/shorts/`)

## Requirements

- **.NET SDK 8.0**
- **`yt-dlp` available on your PATH**
  - Verify with:
    - `yt-dlp --version`

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
        "DownloadResolution": "1080"
      }
    ],
    "DownloadPath": "C:/Users/<you>/Videos/TubePulse",
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
- **`TubePulse:DownloadPath`**: Root directory where videos are downloaded.
  - Downloads land in: `DownloadPath/<ChannelName>/`
- **`TubePulse:CachePath`**: Directory where cache JSON files are stored.
  - Cache filename pattern: `videoCache_<ChannelName>.json`
- **`TubePulse:PollingTimeoutHours`**: How long to wait between checks.
- **`TubePulse:DownloadResolution`**: Default resolution used when a channel doesn’t specify one.

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

- **`yt-dlp` not found**
  - Ensure `yt-dlp` is installed and available on PATH (`yt-dlp --version`).
- **App prints: `DownloadPath and CachePath must be specified`**
  - Set `TubePulse:DownloadPath` and `TubePulse:CachePath` in `appsettings.json`.
- **Nothing downloads on first run**
  - This is expected: the first run only caches existing videos so it starts downloading on *new* uploads going forward.
- **Downloads failing for unknown reasons**
  - Ensure yt-dlp is updated is it can stop working if too far out of date.

## Notes

- TubePulse filters out YouTube Shorts when listing videos.
- Downloads are performed by `yt-dlp`; the exact format selection string currently targets a max height equal to the configured resolution.