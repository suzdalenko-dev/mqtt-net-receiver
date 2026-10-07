namespace MqttNetService;
using Microsoft.Extensions.Configuration;

public class Worker(ILogger<Worker> logger, IConfiguration configuration, IHostEnvironment environment) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Falta configurar ConnectionStrings:Postgres.");

        logger.LogInformation("Worker iniciado a las {Time}. Entorno: {Environment}", DateTimeOffset.Now, environment.EnvironmentName);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                 logger.LogInformation(
                    "SUZDALENKO {Time}. Entorno: {Environment}"+ $" {connectionString}",
                    DateTimeOffset.Now,
                    environment.EnvironmentName);
            }
            await Task.Delay(22000, stoppingToken);
        }
    }
}