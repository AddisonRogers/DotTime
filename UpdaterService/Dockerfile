FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["UpdaterService/UpdaterService.csproj", "UpdaterService/"]
RUN dotnet restore "UpdaterService/UpdaterService.csproj"
COPY . .
WORKDIR "/src/UpdaterService"
RUN dotnet build "UpdaterService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "UpdaterService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UpdaterService.dll"]
