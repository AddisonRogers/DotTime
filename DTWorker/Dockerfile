FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DTWorker/DTWorker.csproj", "DTWorker/"]
RUN dotnet restore "DTWorker/DTWorker.csproj"
COPY . .
WORKDIR "/src/DTWorker"
RUN dotnet build "DTWorker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DTWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DTWorker.dll"]
