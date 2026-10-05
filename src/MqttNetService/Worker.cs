
namespace MqttNetService;
using Microsoft.Extensions.Configuration;
public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Falta configurar ConnectionStrings:Postgres.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("SUZDALENKO {time}"+ $"{connectionString}", DateTimeOffset.Now);
            }
            await Task.Delay(22000, stoppingToken);
        }
    }
}
