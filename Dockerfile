FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TimeTrack.csproj", "./"]
RUN dotnet restore "TimeTrack.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "TimeTrack.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TimeTrack.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TimeTrack.dll"]
