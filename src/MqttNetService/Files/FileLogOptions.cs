namespace MqttNetService.Files;

public sealed class FileLogOptions
{
    public const string SectionName = "FileLog";
    public string Directory {get; set;} = "data";
}