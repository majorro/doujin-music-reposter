FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DoujinMusicReposter.Telegram/DoujinMusicReposter.Telegram.csproj", "DoujinMusicReposter.Telegram/"]
RUN dotnet restore "DoujinMusicReposter.Telegram/DoujinMusicReposter.Telegram.csproj"
COPY . .
WORKDIR "/src/DoujinMusicReposter.Telegram"
RUN dotnet build "DoujinMusicReposter.Telegram.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DoujinMusicReposter.Telegram.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DoujinMusicReposter.Telegram.dll"]
