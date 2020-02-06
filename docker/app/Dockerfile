# escape=`

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build

COPY . C:\src
RUN dotnet build C:\src `
    && dotnet publish --output C:\app C:\src


FROM mcr.microsoft.com/windows/servercore:ltsc2019

COPY --from=build C:\app C:\app
RUN mkdir C:\configs `
    && mklink C:\app\osu!BeatmapMirror.cfg C:\configs\osu!BeatmapMirror.cfg `
    && mklink C:\app\osu!BeatmapMirror.cfg.cfsession C:\configs\osu!BeatmapMirror.cfg.cfsession

ADD https://dotnetbinaries.blob.core.windows.net/servicemonitor/2.0.1.6/ServiceMonitor.exe C:\

EXPOSE 80 443
CMD C:\app\Manager.exe
