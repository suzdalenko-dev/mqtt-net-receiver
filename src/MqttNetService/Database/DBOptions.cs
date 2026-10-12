namespace MqttNetService.Database;
public sealed class DBOptions
{
    public string Host {get; set;} = "";
    public int Port {get; set;} = 5432;
    public string Basename {get; set;} = "";
    public string Username {get; set;} = "";
    public string Password {get; set;} = "";
    public string Table {get; set;} = "mqtt_record_lines";
}