# 1. Capa de ejecución (.NET 9 ASP.NET)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# 2. Capa de compilación (.NET 9 SDK)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Desactivar telemetría y desactivar reutilización de nodos para ahorrar RAM
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV MSBUILDDISABLENODEREUSE=1

# Copiar el archivo de proyecto y restaurar
COPY ["SaasBarberia/SaasBarberia.csproj", "SaasBarberia/"]
RUN dotnet restore "SaasBarberia/SaasBarberia.csproj"

# Copiar el resto del código
COPY . .
WORKDIR "/src/SaasBarberia"

# Publicar directamente en un solo paso para reducir capas y consumo de RAM
RUN dotnet publish "SaasBarberia.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:BuildNodeBundles=false \
    /p:BuildInParallel=false

# 3. Capa final de producción
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
# Desactivar Server GC para evitar alto consumo de memoria y Error 139 (Segfault) en Render
ENV DOTNET_gcServer=0

ENTRYPOINT ["dotnet", "SaasBarberia.dll"]