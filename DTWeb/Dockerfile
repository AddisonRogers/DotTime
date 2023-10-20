FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DTWeb/DTWeb.csproj", "DTWeb/"]
RUN dotnet restore "DTWeb/DTWeb.csproj"
COPY . .
WORKDIR "/src/DTWeb"
RUN dotnet build "DTWeb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DTWeb.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DTWeb.dll"]
