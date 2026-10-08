using MqttNetService.Files;
using MqttNetService.Mqtt;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------
// MQTT configuration
// ---------------------------------------------------------

builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<FileLogOptions>(builder.Configuration.GetSection("FileLog"));
builder.Services.AddSingleton<LogQueue>();
builder.Services.Configure<HostOptions>(options =>
{
   options.ServicesStartConcurrently = false;
   options.ShutdownTimeout = TimeSpan.FromSeconds(60);
   options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
});
// ---------------------------------------------------------
// Background services
// ---------------------------------------------------------

builder.Services.AddHostedService<FileWriterService>();
builder.Services.AddHostedService<MqttReceiverService>();



// builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();


/*
Work to be implemented TODO de forma INDUSTIAL/SIMPLE/ESTABLE !!!
    1. Secure/stable/reconnections/industrial/simple connections for MQTT   (consider asyncio, thread)
    2. Write received data to the log file "data/YEAR/MONTH.log"            (consider asyncio, thread)
    3. Delete the log from the previos year "data/YEAR-1"                   (consider asyncio, thread)
    4. Create singlenton persisten/higthPerfomance DB POSTGRE connection
        Create data base column id, date_utc, date_local, topic, value
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
├── FileWriterService          ← más adelante
│
└── PostgreSqlWriterService    ← más adelante
*/
