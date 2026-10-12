using System.Globalization;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace MqttNetService.Database;

public sealed class PostgreSqlWriterService : BackgroundService
{
    private readonly ILogger<PostgreSqlWriterService> _logger;
    private readonly DbQueue _queue;
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _insertSql;
    private readonly TimeSpan _retryDelay;
    private readonly CancellationTokenSource _shutdownTimeout = new();

    public PostgreSqlWriterService(
        ILogger<PostgreSqlWriterService> logger,
        DbQueue queue,
        NpgsqlDataSource dataSource,
        IOptions<DBOptions> options)
    {
        _logger = logger;
        _queue = queue;
        _dataSource = dataSource;
        _retryDelay = TimeSpan.FromSeconds(options.Value.RetryDelaySeconds);

        // Los parámetros sirven para valores. El nombre de tabla se cita aparte.
        using var sqlBuilder = new NpgsqlCommandBuilder();
        string table = sqlBuilder.QuoteIdentifier(options.Value.Table);
        _insertSql = $"""
            INSERT INTO public.{table} (date_utc, date_local, topic, content)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT (date_utc) DO NOTHING
            """;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Escritor PostgreSQL iniciado.");

            // La cancelación normal del Host no interrumpe el vaciado de la cola.
            // Solo se cancela al agotar el plazo de apagado.
            await foreach (Message message in _queue.Reader.ReadAllAsync(_shutdownTimeout.Token))
            {
                await InsertWithRetryAsync(message, _shutdownTimeout.Token);
            }

            _logger.LogInformation("Escritor PostgreSQL detenido. Cola vaciada.");
        }
        catch (OperationCanceledException) when (_shutdownTimeout.IsCancellationRequested)
        {
            Environment.ExitCode = 1;
            _logger.LogError("Se agotó el plazo de apagado con mensajes pendientes de guardar en PostgreSQL.");
        }
        catch (Exception exception)
        {
            _queue.Complete(exception);
            Environment.ExitCode = 1;
            _logger.LogCritical(exception, "El escritor PostgreSQL ha fallado. Se detendrá la aplicación.");
            throw;
        }
    }

    private async Task InsertWithRetryAsync(Message message, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                // CreateCommand toma una conexión del pool y la devuelve al terminar.
                await using var command = _dataSource.CreateCommand(_insertSql);

                // La tabla existente almacena las fechas como TEXT con milisegundos.
                command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
                    Value = message.DateUtc.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) });
                command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
                    Value = message.DateLocal.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) });
                command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = message.Topic });
                command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = message.Content });

                int inserted = await command.ExecuteNonQueryAsync(cancellationToken);
                if (inserted == 0)
                {
                    _logger.LogWarning(
                        "PostgreSQL no insertó el mensaje: ya existe date_utc={DateUtc:yyyy-MM-dd HH:mm:ss.fff}. Topic={Topic}",
                        message.DateUtc, message.Topic);
                }

                return;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                _logger.LogWarning(exception,
                    "Fallo temporal de PostgreSQL. Se reintentará el MISMO mensaje en {Delay} segundos.",
                    _retryDelay.TotalSeconds);
                await Task.Delay(_retryDelay, cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception exception) =>
        exception is NpgsqlException { IsTransient: true } or TimeoutException;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Complete();
        using var registration = cancellationToken.Register(() => _shutdownTimeout.Cancel());
        await base.StopAsync(cancellationToken);

        if (ExecuteTask is { IsCompleted: false })
        {
            _shutdownTimeout.Cancel();
            Environment.ExitCode = 1;
        }
    }

    public override void Dispose()
    {
        _shutdownTimeout.Cancel();
        base.Dispose();
        _shutdownTimeout.Dispose();
    }
}
