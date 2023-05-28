#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
RUN apt-get update \
	&& apt-get install -y ffmpeg

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["PlaybackFixBot/PlaybackFixBot.csproj", "PlaybackFixBot/"]
RUN dotnet restore "PlaybackFixBot/PlaybackFixBot.csproj"
COPY . .
WORKDIR "/src/PlaybackFixBot"
RUN dotnet build "PlaybackFixBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PlaybackFixBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PlaybackFixBot.dll"]