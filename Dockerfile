FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/MqtNetService/MqtNetService.csproj ./
RUN dotnet restore MqtNetService.csproj \
    -c Release \
    -o /out \
    --no-restore \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

COPY --from=build /out/ ./
USER $APP_UID

ENTRYPOINT [ "dotnet", "MqtNetService.dll" ]