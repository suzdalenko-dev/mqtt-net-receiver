# Paso 4: guardar MQTT en PostgreSQL

El receptor crea un `Message` y entrega ese mismo objeto a dos colas. `FileWriterService`
consume `LogQueue` y escribe el JSONL. `PostgreSqlWriterService` consume `DbQueue` y
guarda en PostgreSQL. Los escritores trabajan de forma independiente.

## Qué hace cada pieza

| Pieza | Trabajo | Equivalente en Python |
| --- | --- | --- |
| `DBOptions` | Lee servidor, base, usuario y contraseña desde la sección `DB` | Variables de configuración |
| `DbQueue` | Guarda hasta 11.111 mensajes pendientes en memoria | `DB_QUEUE` |
| `NpgsqlDataSource` registrado como singleton | Mantiene un único pool, con una conexión física como máximo | `get_postgres_pool()` |
| `PostgreSqlWriterService` | Lee la cola, ejecuta el INSERT y reintenta errores temporales | `db_writer()` |

No hace falta `DbMessage`: ambas colas contienen directamente `Message`.

En `MqttReceiverService`:

```csharp
await _logQueue.EnqueueAsync(message, _enqueueCancellation.Token);
await _dbQueue.EnqueueAsync(message, _enqueueCancellation.Token);
```

Estas llamadas esperan a que haya espacio en las colas. No esperan a que se escriba
el fichero o se confirme el INSERT. Si PostgreSQL está caído y se llena `DbQueue`,
el receptor espera; no descarta el siguiente mensaje para hacerle sitio a otro.

En el escritor:

```csharp
await using var command = _dataSource.CreateCommand(_insertSql);
// Se añaden los cuatro parámetros.
await command.ExecuteNonQueryAsync(cancellationToken);
```

Npgsql toma una conexión del pool para ejecutar el comando y la devuelve después.
Reutiliza la conexión física cuando sigue sana; si se rompe, puede abrir otra.
El objeto compartido es el pool, no un `NpgsqlConnection` usado simultáneamente.

## Configuración

Conserva tu configuración MQTT y añade la sección `DB` de
`src/MqttNetService/appsettings.example.json` a tu `appsettings.json` privado.
Si empiezas desde cero, copia el ejemplo con ese nombre y rellena los valores vacíos.
`appsettings.example.json` es una plantilla: el Host no la carga automáticamente.

Se mantienen los nombres `Host`, `Port`, `Basename`, `Username`, `Password` y `Table`
de `DBOptions`. `Table` es un nombre de tabla dentro del esquema `public`, sin prefijo
`public.`. `RetryDelaySeconds` es opcional y vale 1 por defecto.

También puedes usar variables de entorno como `DB__Host`, `DB__Basename`,
`DB__Username` y `DB__Password`. En Compose deben estar dentro de `environment`
o de un fichero declarado en `env_file`; un `.env` por sí solo no las introduce en
el contenedor. No subas credenciales al repositorio.

`DateLocal` conserva la hora local del proceso, igual que antes. Configura
`TZ=Europe/Madrid` en el contenedor si quieres la hora de Cantabria.

## Tabla y formato

La implementación utiliza la tabla existente con `date_utc`, `date_local`, `topic`
y `content` de tipo `TEXT`. Las fechas tienen formato `yyyy-MM-dd HH:mm:ss.fff`.
`id` debe generarse automáticamente en PostgreSQL, porque no se incluye en el INSERT.
`sql/create_mqtt_record_lines.sql` permite preparar una base nueva de pruebas;
la aplicación no crea ni modifica tablas al arrancar.

El contenido se guarda tal como llega decodificado de MQTT, sea JSON o texto.
No se convierte un JSON a la representación de un diccionario de Python.

Se conserva `ON CONFLICT (date_utc) DO NOTHING`, por lo que se necesita una
restricción UNIQUE sobre `date_utc`. **Dos mensajes distintos recibidos en el mismo
milisegundo pueden colisionar:** el segundo no se inserta y se escribe una advertencia.
Esta clave conserva el comportamiento del Python y permite reintentar el mismo
INSERT después de una respuesta perdida, pero no identifica de forma única cada
evento MQTT ni elimina duplicados de red. Cambiar a un identificador de evento
requiere una migración aparte.

## Fallos y parada

- Si hay un error temporal de conexión o un timeout, se reintenta el mismo mensaje
  cada `RetryDelaySeconds`. No se salta al siguiente tras 11 intentos.
- Un error permanente, como tabla inexistente, contraseña incorrecta o permisos
  insuficientes, cierra la cola y provoca la parada del Host con código de error.
- Al parar, primero se detiene MQTT y se desbloquea cualquier espera en las colas.
  Después se completa y vacía la cola de PostgreSQL, y por último la de ficheros.
- Se respeta el plazo de apagado predeterminado del Host. Si se agota, se cancela
  la escritura de PostgreSQL y se marca código de error.

Las colas están en memoria: un corte de proceso, de corriente o un plazo de apagado
agotado puede perder mensajes pendientes. El fichero JSONL se conserva, pero esta
implementación no lo reproduce automáticamente hacia PostgreSQL. La confirmación
MQTT ocurre al encolar, no al terminar el INSERT.

## Comprobarlo

```bash
dotnet build src/MqttNetService/MqttNetService.csproj -c Release
dotnet run --project src/MqttNetService
```

Publica un mensaje en el topic configurado y comprueba el JSONL y la tabla:

```sql
SELECT id, date_utc, date_local, topic, content
FROM public.mqtt_record_lines
ORDER BY id DESC
LIMIT 10;
```

En una base de pruebas, detén PostgreSQL, publica algunos mensajes y vuelve a
arrancarlo: deben guardarse en orden al recuperarse. Comprueba también Ctrl+C con
mensajes pendientes. Para Docker se mantiene `docker compose up -d --build` y
`docker compose logs --tail 50 -f mqtt-net-service`.

Documentación de referencia: [uso y pool de Npgsql](https://www.npgsql.org/doc/basic-usage.html).
