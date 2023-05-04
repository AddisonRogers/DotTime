FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["TimeTrackService/TimeTrackService.csproj", "TimeTrackService/"]
RUN dotnet restore "TimeTrackService/TimeTrackService.csproj"
COPY . .
WORKDIR "/src/TimeTrackService"
RUN dotnet build "TimeTrackService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TimeTrackService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TimeTrackService.dll"]
