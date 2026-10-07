using MqttNetService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();


/*
Work to be implemented:
    1. Secure/stable/reconnections/industrial/simple connections for MQTT   (consider asyncio, thread)
    2. Write received data to the log file "data/YEAR/MONTH.log"            (consider asyncio, thread)
    3. Delete the log from the previos year "data/YEAR-1"                   (consider asyncio, thread)
    4. Create singlenton persisten/higthPerfomance DB POSTGRE connection
        Create data base column id, date_utc, date_local, topic, value
   c)
*/
