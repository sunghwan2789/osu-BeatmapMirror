# escape=`

FROM mcr.microsoft.com/dotnet/core/sdk:3.1.202 AS build

COPY . C:\src
RUN dotnet build C:\src `
    && dotnet publish --output C:\app C:\src


FROM mcr.microsoft.com/windows/servercore:ltsc2019

COPY --from=build C:\app C:\app
RUN mkdir C:\configs `
    && mklink C:\app\osu!BeatmapMirror.cfg C:\configs\osu!BeatmapMirror.cfg `
    && mklink C:\app\osu!BeatmapMirror.cfg.cfsession C:\configs\osu!BeatmapMirror.cfg.cfsession

EXPOSE 80 443
CMD C:\app\Manager.exe
