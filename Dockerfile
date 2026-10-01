# Instrucciónes para FABRICAR una Imagen
# Imagen es un sistema de archivos preparado + metadatos que indican como usarlo

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS pepito

WORKDIR /src
COPY src/MqttNetService/MqttNetService.csproj ./

RUN dotnet restore MqttNetService.csproj
COPY src/MqttNetService/ ./
RUN dotnet publish MqttNetService.csproj -c Release -o /out --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=pepito /out/ ./

USER $APP_UID
ENTRYPOINT ["dotnet", "MqttNetService.dll"]


