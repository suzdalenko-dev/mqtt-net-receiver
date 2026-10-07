namespace MqttNetService.Files;

public sealed class FileLogOptions
{
    public const string SectionName = "FileLog";
    public string Directory {get; set;} = "data";
    public int QueueCapacity {get; set;} = 11_111;
}