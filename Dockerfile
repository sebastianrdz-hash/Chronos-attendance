# Imagen de la API para plataformas de contenedores (Render, Fly.io, Azure Container Apps).
# El contexto de compilación es la raíz del repositorio para que aplique el .dockerignore.

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS compilacion
WORKDIR /origen

# Los .csproj se copian antes que el código: mientras las dependencias no cambien,
# Docker reutiliza la capa del restore y el redespliegue baja de minutos a segundos.
COPY Backend/Directory.Build.props ./Backend/
COPY Backend/src/Chronos.Domain/Chronos.Domain.csproj ./Backend/src/Chronos.Domain/
COPY Backend/src/Chronos.Infrastructure/Chronos.Infrastructure.csproj ./Backend/src/Chronos.Infrastructure/
COPY Backend/src/Chronos.Api/Chronos.Api.csproj ./Backend/src/Chronos.Api/
RUN dotnet restore Backend/src/Chronos.Api/Chronos.Api.csproj

COPY Backend/src/ ./Backend/src/
RUN dotnet publish Backend/src/Chronos.Api/Chronos.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /aplicacion

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /aplicacion

# Alpine trae solo la cultura invariante y el proyecto declara es-MX, necesario para
# que el formato de fechas y números no cambie según dónde corra el contenedor.
RUN apk add --no-cache icu-libs tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Un usuario sin privilegios limita el daño si alguien logra ejecutar código dentro.
RUN adduser --disabled-password --no-create-home --uid 64198 chronos
USER chronos

COPY --from=compilacion /aplicacion .

# Render publica el puerto real en la variable PORT; 8080 es el respaldo para
# ejecutar la imagen a mano o desde docker-compose.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Chronos.Api.dll"]
