FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DoujinMusicReposter.App/DoujinMusicReposter.App.csproj", "DoujinMusicReposter.App/"]
COPY ["DoujinMusicReposter.Persistence/DoujinMusicReposter.Persistence.csproj", "DoujinMusicReposter.Persistence/"]
COPY ["DoujinMusicReposter.Telegram/DoujinMusicReposter.Telegram.csproj", "DoujinMusicReposter.Telegram/"]
COPY ["DoujinMusicReposter.Vk/DoujinMusicReposter.Vk.csproj", "DoujinMusicReposter.Vk/"]
RUN dotnet restore "DoujinMusicReposter.App/DoujinMusicReposter.App.csproj"
COPY . .
WORKDIR "/src/DoujinMusicReposter.App"
RUN dotnet build "./DoujinMusicReposter.App.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DoujinMusicReposter.App.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DoujinMusicReposter.App.dll"]
