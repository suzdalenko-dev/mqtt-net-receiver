using Microsoft.Extensions.Options;
using MqttNetService.Database;
using MqttNetService.Files;
using MqttNetService.Mqtt;
using Npgsql;

// Preparar configuracion, logs y registros de servicios
var builder = Host.CreateApplicationBuilder(args);
// Asociar la seccion MQTT con la clase MqttOptions
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
// Asocias la seccion Filelog con FileLogOptions
builder.Services.Configure<FileLogOptions>(builder.Configuration.GetSection("FileLog"));
builder.Services.AddOptions<DBOptions>()
    .Bind(builder.Configuration.GetSection("DB"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Falta DB:Host.")
    .Validate(options => options.Port is >= 1 and <= 65535, "DB:Port debe estar entre 1 y 65535.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Basename), "Falta DB:Basename.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "Falta DB:Username.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Table), "Falta DB:Table.")
    .Validate(options => options.RetryDelaySeconds > 0, "DB:RetryDelaySeconds debe ser mayor que 0.")
    .ValidateOnStart();

// Un pool compartido durante toda la vida del Host.
// No conecta hasta que el escritor necesita ejecutar su primer INSERT.
builder.Services.AddSingleton<NpgsqlDataSource>(services =>
{
    var options = services.GetRequiredService<IOptions<DBOptions>>().Value;
    var connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = options.Host,
        Port = options.Port,
        Database = options.Basename,
        Username = options.Username,
        Password = options.Password,
        ApplicationName = "mqtt-net-receiver",
        Pooling = true,
        MinPoolSize = 1,
        MaxPoolSize = 1,
        Timeout = 5,
        CommandTimeout = 10,
        KeepAlive = 30
    };
    return NpgsqlDataSource.Create(connectionString.ConnectionString);
});

// Cada escritor tiene su propia cola. Ambas reciben el mismo Message.
builder.Services.AddSingleton<LogQueue>();
builder.Services.AddSingleton<DbQueue>();

// Registrar trabajos que deben permanecer funcionando
builder.Services.AddHostedService<FileWriterService>();
builder.Services.AddHostedService<PostgreSqlWriterService>();
// Se registra el receptor al final para detenerlo ANTES de vaciar las colas.
builder.Services.AddHostedService<MqttReceiverService>();



// builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();


/*
Work to be implemented TODO de forma INDUSTIAL/SIMPLE/ESTABLE !!!
    1. Secure/stable/reconnections/industrial/simple connections for MQTT   (consider asyncio, thread)
    2. Write received data to the log file "data/YEAR/MONTH.log"            (consider asyncio, thread)
    3. Delete the log from the previos year "data/YEAR-1"                   (consider asyncio, thread)
    4. Implementado: pool Npgsql singleton + DbQueue + PostgreSqlWriterService.
        Tabla existente: id, date_utc, date_local, topic, content.
   c)

main.py
   │
   ├── crea cliente MQTT
   ├── username/password
   ├── MQTT 3.1.1
   ├── clean_session=False
   ├── reconnect 1..60 s
   ├── connect_async()
   ├── subscribe()
   └── loop_forever()

Generic Host
│
├── MqttReceiverService        ← PUNTO 1
│      ├── conexión
│      ├── autenticación
│      ├── TLS opcional
│      ├── MQTT 3.1.1
│      ├── persistent session
│      ├── subscribe
│      ├── keepalive
│      ├── timeout
│      ├── reconexión
│      ├── backoff
│      └── shutdown limpio
│
├── FileWriterService          ← PUNTOS 2 Y 3
│
└── PostgreSqlWriterService    ← PUNTO 4
*/
