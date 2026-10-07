namespace MqttNetService.Mqtt;

public sealed class MqttOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1883;

    public string ClientId { get; set; } = "";

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    public string Topic { get; set; } = "";

    public int Qos { get; set; } = 1;

    public int KeepAliveSeconds { get; set; } = 30;

    public int ConnectTimeoutSeconds { get; set; } = 10;

    public int ReconnectMinSeconds { get; set; } = 1;
    public int ReconnectMaxSeconds { get; set; } = 60;

    public bool UseTls { get; set; } = false;
}