# Imagen multi-arquitectura: funciona igual en Intel/AMD (Windows) y ARM (Mac Apple Silicon).
# Se construye desde la raíz del repositorio:  docker build -f src/OrderAgent.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1) Restaurar paquetes primero (se cachea mientras no cambien los .csproj)
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/OrderAgent.Domain/OrderAgent.Domain.csproj src/OrderAgent.Domain/
COPY src/OrderAgent.Agent/OrderAgent.Agent.csproj src/OrderAgent.Agent/
COPY src/OrderAgent.Infrastructure/OrderAgent.Infrastructure.csproj src/OrderAgent.Infrastructure/
COPY src/OrderAgent.Api/OrderAgent.Api.csproj src/OrderAgent.Api/
RUN dotnet restore src/OrderAgent.Api/OrderAgent.Api.csproj

# 2) Compilar y publicar
COPY src/ src/
RUN dotnet publish src/OrderAgent.Api/OrderAgent.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Usuario sin privilegios incluido en las imágenes oficiales de .NET
USER $APP_UID
ENTRYPOINT ["dotnet", "OrderAgent.Api.dll"]
