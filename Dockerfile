FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/YaTrackerTelegramBot/YaTrackerTelegramBot.csproj src/YaTrackerTelegramBot/
RUN dotnet restore src/YaTrackerTelegramBot/YaTrackerTelegramBot.csproj
COPY src/YaTrackerTelegramBot/ src/YaTrackerTelegramBot/
RUN dotnet publish src/YaTrackerTelegramBot/YaTrackerTelegramBot.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "YaTrackerTelegramBot.dll"]
