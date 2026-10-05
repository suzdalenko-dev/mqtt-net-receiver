using MqttNetService;
using MqttNetService.Models;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
var host = builder.Build();

Console.WriteLine($"Carpeta de configuración: {builder.Environment.ContentRootPath}");
Console.WriteLine($"Entorno: {builder.Environment.EnvironmentName}");
Console.WriteLine($"Nivel de log: {builder.Configuration["Logging:LogLevel:Default"]}");


Person p = new Person("suzdalenko");

IEscritor escritor = new EscritorConsola();
// escritor.Escribir("-#--#- #-- #-----------------------------------------------------------------------");
Receptor receptor = new Receptor(escritor);
receptor.Recibir("##########################################################Mensaje MQTT de prueba");

Caja<int>   cajaNumero = new Caja<int>(25);
Caja<string> cajaTexto = new Caja<string>("MQTT");

Console.WriteLine(cajaNumero.Argument); // 25.
Console.WriteLine(cajaTexto.Argument);  // MQTT.


Dictionary<string, decimal> precios = new Dictionary<string, decimal>();

precios["ART001"] = 12.95m;
precios["ART002"] = 8.50m;

Console.WriteLine(precios["ART001"]); // 12.95.


host.Run();

public class Caja<TipoVariable>
{
    public TipoVariable Argument { get; }

    public Caja(TipoVariable value)
    {
        Argument = value;
    }
}


public interface IEscritor
{
    void Escribir(string contenido);
}

public class EscritorConsola : IEscritor
{
    public void Escribir(string contenido)
    {
        Console.WriteLine(contenido);
    }
}

public class Receptor
{
    private readonly IEscritor _escritor;

    public Receptor(IEscritor escritor)
    {
        _escritor = escritor;
    }

    public void Recibir(string contenido)
    {
        _escritor.Escribir(contenido);
    }
}