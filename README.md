# osu! Beatmap Mirror

> Sync latest osu! beatmapsets from osu! website.

## 📖 Guide

Currently, **osu! Beatmap Mirror** needs latest version of Windows.

### Clone

```
git clone --recurse-submodule git@github.com:sunghwan2789/osu-BeatmapMirror.git
cd osu-BeatmapMirror
```

### Configure

```
copy storage\configs\osu!BeatmapMirror.cfg.example storage\configs\osu!BeatmapMirror.cfg
notepad storage\configs\osu!BeatmapMirror.cfg
```

### Install

There are dependencies for **osu! Beatmap Mirror** to run correctly. Please install below programs to develop or use in production.

#### For Development

- **.NET Core 3.1 SDK**: https://dotnet.microsoft.com/download/dotnet-core/3.1

#### For Production and Testing

- **Docker Desktop**: https://www.docker.com/products/docker-desktop
- **Docker Compose**: https://docs.docker.com/compose/install/

### Run

```
docker-compose up -d --build
```

### See

```
docker-compose exec db mysql -uroot -Dobm -e "SELECT * FROM gosu_sets \G"
type storage\logs\obm.log
dir storage\beatmapsets
```

### Stop

```
docker-compose stop
```

### Reset

```
docker-compose down -v
```

## 💡 About Projects

- **Bot**: Sync beatmapsets automatically.
- **Manager**: Schedule **Bot** to run periodically and let osu! users register a beatmapset manually.
- **osu**: osu! client that is customized for osu! Beatmap Mirror. Parse a metadata of beatmap.
- **Summary**: Deprecated. Summarize downloads count of a beatmapset.
- **Utility**: Common functions.
