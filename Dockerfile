# 1. Capa de ejecución (.NET 9 ASP.NET)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# 2. Capa de compilación (.NET 9 SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Desactivar telemetría y reutilización de nodos para ahorrar RAM
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV MSBUILDDISABLENODEREUSE=1

# Copiar el archivo de proyecto (CUIDADO CON LAS MAYÚSCULAS: SaaSBarberia.csproj)
COPY ["SaaSBarberia.csproj", "./"]
RUN dotnet restore "SaaSBarberia.csproj"

# Copiar el resto del código de la raíz
COPY . .

# Publicar la aplicación
RUN dotnet publish "SaaSBarberia.csproj" \
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
# Desactivar Server GC para evitar alto consumo de RAM en Render
ENV DOTNET_gcServer=0

ENTRYPOINT ["dotnet", "SaaSBarberia.dll"]