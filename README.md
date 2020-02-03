# osu! Beatmap Mirror

> Sync latest osu! beatmapsets from osu! website.

## How To Clone This Repo

```
git clone --recurse-submodule git@github.com:sunghwan2789/osu-BeatmapMirror.git
```

## Requirements

* **.NET Core 3.1 SDK**: https://dotnet.microsoft.com/download/dotnet-core/3.1
* **Creating database and configuration file**: see #1

## About Projects

* **Bot**: Sync beatmapsets automatically.
* **Manager**: Schedule **Bot** to run periodically and let osu! users register a beatmapset manually.
* **osu**: osu! client that is customized for osu! Beatmap Mirror. Parse a metadata of beatmap.
* **Summary**: Deprecated. Summarize downloads count of a beatmapset.
* **Utility**: Common functions.
