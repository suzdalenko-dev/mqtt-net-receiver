using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
namespace MqttNetService.Mqtt;

public sealed class MqttReceiverService : BackgroundService
{
    private readonly ILogger<MqttReceiverService> _logger;
    private readonly MqttOptions _options;
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _mqttClientOptions;

    private bool _stopping;


    public MqttReceiverService(ILogger<MqttReceiverService> logger, IOptions<MqttOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        ValidateOptions();

        var factory = new MqttClientFactory();

        _mqttClient = factory.CreateMqttClient();

        _mqttClientOptions = BuildClientOptions();


        // Estos eventos NO controlan la reconexión.
        // Solamente informan de lo que ha ocurrido.
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

        // De momento solamente comprobamos que llegan mensajes.
        // En el punto 2 este método enviará los mensajes a una cola.
        _mqttClient.ApplicationMessageReceivedAsync +=
            OnApplicationMessageReceivedAsync;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Servicio MQTT iniciado. Broker={Host}:{Port}, ClientId={ClientId}, Topic={Topic}",
            _options.Host,
            _options.Port,
            _options.ClientId,
            _options.Topic);


        var reconnectAttempt = 0;


        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // ---------------------------------------------------------
                // ESTADO 1:
                // Ya estamos conectados.
                // ---------------------------------------------------------
                if (_mqttClient.IsConnected)
                {
                    reconnectAttempt = 0;

                    await Task.Delay(
                        TimeSpan.FromSeconds(1),
                        stoppingToken);

                    continue;
                }


                // ---------------------------------------------------------
                // ESTADO 2:
                // No estamos conectados.
                // Intentamos conectar.
                // ---------------------------------------------------------
                try
                {
                    await ConnectAndSubscribeAsync(stoppingToken);

                    reconnectAttempt = 0;
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    // El servicio está apagándose.
                    break;
                }
                catch (Exception exception)
                {
                    reconnectAttempt++;

                    TimeSpan delay =
                        CalculateReconnectDelay(reconnectAttempt);

                    _logger.LogWarning(
                        exception,
                        "No se pudo conectar al broker MQTT. " +
                        "Intento={Attempt}. Nuevo intento en {DelaySeconds:F1} segundos.",
                        reconnectAttempt,
                        delay.TotalSeconds);


                    await Task.Delay(delay, stoppingToken);
                }
            }
        }
        finally
        {
            // ---------------------------------------------------------
            // Ctrl+C
            // docker stop
            // systemctl stop
            // SIGTERM
            //
            // Acabamos aquí.
            // ---------------------------------------------------------

            await DisconnectSafelyAsync();
        }
    }


    private async Task ConnectAndSubscribeAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Intentando conexión MQTT con {Host}:{Port}...", _options.Host, _options.Port);

        MqttClientConnectResult connectResult = await _mqttClient.ConnectAsync(_mqttClientOptions, cancellationToken);
        _logger.LogInformation("MQTT conectado. Result={ResultCode}, SessionPresent={SessionPresent}", connectResult.ResultCode, connectResult.IsSessionPresent);


        try
        {
            await SubscribeAsync(cancellationToken);
        }
        catch
        {
            await _mqttClient.TryDisconnectAsync();
            throw;
        }
    }


    private async Task SubscribeAsync(
    CancellationToken cancellationToken)
    {
        MqttQualityOfServiceLevel qos =
            GetQualityOfServiceLevel();


        _logger.LogInformation(
            "Suscribiendo a Topic={Topic}, QoS={Qos}",
            _options.Topic,
            (int)qos);


        MqttClientSubscribeResult result =
            await _mqttClient.SubscribeAsync(
                _options.Topic,
                qos,
                cancellationToken);


        foreach (MqttClientSubscribeResultItem item in result.Items)
        {
            /*
             * Los códigos >= 0x80 representan rechazo/error.
             */

            if ((int)item.ResultCode >= 0x80)
            {
                throw new InvalidOperationException(
                    $"El broker MQTT rechazó la suscripción " +
                    $"'{item.TopicFilter.Topic}'. " +
                    $"Result={item.ResultCode}");
            }


            _logger.LogInformation(
                "Suscripción MQTT aceptada. Topic={Topic}, Result={Result}",
                item.TopicFilter.Topic,
                item.ResultCode);
        }
    }


    private Task OnConnectedAsync(
        MqttClientConnectedEventArgs args)
    {
        _logger.LogInformation(
            "Evento MQTT Connected recibido.");

        return Task.CompletedTask;
    }


    private Task OnDisconnectedAsync(
        MqttClientDisconnectedEventArgs args)
    {
        if (_stopping)
        {
            _logger.LogInformation(
                "MQTT desconectado durante la parada del servicio.");

            return Task.CompletedTask;
        }


        if (args.Exception is not null)
        {
            _logger.LogWarning(
                args.Exception,
                "Conexión MQTT perdida. Reason={Reason}",
                args.Reason);
        }
        else
        {
            _logger.LogWarning(
                "Conexión MQTT perdida. Reason={Reason}",
                args.Reason);
        }


        /*
         * IMPORTANTE:
         *
         * NO reconectamos aquí.
         *
         * El bucle ExecuteAsync detectará:
         *
         *      _mqttClient.IsConnected == false
         *
         * y realizará la reconexión.
         */

        return Task.CompletedTask;
    }


    private Task OnApplicationMessageReceivedAsync(
    MqttApplicationMessageReceivedEventArgs args)
    {
        string payload = args.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;

        _logger.LogInformation(
            "MQTT RX | Topic={Topic} | QoS={Qos} | Retain={Retain} | Payload={Payload}",
            args.ApplicationMessage.Topic,
            (int)args.ApplicationMessage.QualityOfServiceLevel,
            args.ApplicationMessage.Retain,
            payload);

        return Task.CompletedTask;
    }


    private MqttClientOptions BuildClientOptions()
    {
        var builder =
            new MqttClientOptionsBuilder()

                // MQTT 3.1.1, igual que tu Python.
                .WithProtocolVersion(
                    MqttProtocolVersion.V311)

                .WithClientId(
                    _options.ClientId)

                .WithTcpServer(
                    _options.Host,
                    _options.Port)

                /*
                 * IMPORTANTE:
                 *
                 * false = sesión persistente.
                 *
                 * Equivalente a tu:
                 *
                 * clean_session=False
                 */
                .WithCleanSession(false)

                .WithKeepAlivePeriod(
                    TimeSpan.FromSeconds(
                        _options.KeepAliveSeconds))

                .WithTimeout(
                    TimeSpan.FromSeconds(
                        _options.ConnectTimeoutSeconds));


        // -------------------------------------------------------------
        // Usuario / contraseña
        // -------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            builder.WithCredentials(
                _options.Username,
                _options.Password);
        }


        // -------------------------------------------------------------
        // TLS
        // -------------------------------------------------------------

        if (_options.UseTls)
        {
            builder.WithTlsOptions(
                tls =>
                {
                    /*
                     * Utilizamos la validación normal del SO.
                     *
                     * NO hacemos:
                     *
                     * CertificateValidationHandler(_ => true)
                     *
                     * porque eso desactivaría realmente la seguridad
                     * del certificado.
                     */

                    tls.UseTls();
                });
        }


        return builder.Build();
    }


    private MqttQualityOfServiceLevel
        GetQualityOfServiceLevel()
    {
        return _options.Qos switch
        {
            0 => MqttQualityOfServiceLevel.AtMostOnce,

            1 => MqttQualityOfServiceLevel.AtLeastOnce,

            2 => MqttQualityOfServiceLevel.ExactlyOnce,

            _ => throw new InvalidOperationException(
                $"MQTT QoS inválido: {_options.Qos}")
        };
    }


    private TimeSpan CalculateReconnectDelay(
        int attempt)
    {
        /*
         * Backoff exponencial:
         *
         * 1
         * 2
         * 4
         * 8
         * 16
         * 32
         * 60
         * 60
         * 60...
         */

        int exponent =
            Math.Min(attempt - 1, 10);


        double seconds =
            _options.ReconnectMinSeconds *
            Math.Pow(2, exponent);


        seconds =
            Math.Min(
                seconds,
                _options.ReconnectMaxSeconds);


        /*
         * Pequeño jitter.
         *
         * Por ejemplo:
         *
         * broker vuelve después de un apagón
         *
         * 100 clientes intentan conectar
         * exactamente en el segundo 60.
         *
         * Con jitter se distribuyen ligeramente.
         */

        int jitterMilliseconds =
            Random.Shared.Next(0, 1000);


        return
            TimeSpan.FromSeconds(seconds) +
            TimeSpan.FromMilliseconds(jitterMilliseconds);
    }


    private async Task DisconnectSafelyAsync()
    {
        if (!_mqttClient.IsConnected)
        {
            return;
        }


        _logger.LogInformation(
            "Desconectando MQTT limpiamente...");


        try
        {
            using var timeout =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(5));


            await _mqttClient.DisconnectAsync(
                MqttClientDisconnectOptionsReason.NormalDisconnection,
                cancellationToken: timeout.Token);


            _logger.LogInformation(
                "MQTT desconectado correctamente.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "No fue posible realizar la desconexión MQTT limpia.");
        }
    }


    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        _stopping = true;

        await base.StopAsync(cancellationToken);
    }


    public override void Dispose()
    {
        _mqttClient.Dispose();

        base.Dispose();
    }


    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException(
                "Falta Mqtt:Host.");
        }


        if (_options.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                $"Mqtt:Port inválido: {_options.Port}");
        }


        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException(
                "Falta Mqtt:ClientId.");
        }


        if (string.IsNullOrWhiteSpace(_options.Topic))
        {
            throw new InvalidOperationException(
                "Falta Mqtt:Topic.");
        }


        if (_options.Qos is < 0 or > 2)
        {
            throw new InvalidOperationException(
                $"Mqtt:Qos inválido: {_options.Qos}");
        }


        if (_options.KeepAliveSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Mqtt:KeepAliveSeconds debe ser mayor que 0.");
        }


        if (_options.ConnectTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Mqtt:ConnectTimeoutSeconds debe ser mayor que 0.");
        }


        if (_options.ReconnectMinSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Mqtt:ReconnectMinSeconds debe ser mayor que 0.");
        }


        if (_options.ReconnectMaxSeconds <
            _options.ReconnectMinSeconds)
        {
            throw new InvalidOperationException(
                "Mqtt:ReconnectMaxSeconds no puede ser menor " +
                "que Mqtt:ReconnectMinSeconds.");
        }
    }
}