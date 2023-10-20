FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DTAPI/DTAPI.csproj", "DTAPI/"]
RUN dotnet restore "DTAPI/DTAPI.csproj"
COPY . .
WORKDIR "/src/DTAPI"
RUN dotnet build "DTAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DTAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DTAPI.dll"]
