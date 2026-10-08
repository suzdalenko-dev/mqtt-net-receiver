using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MqttNetService.Files;

public sealed class FileWriterService : BackgroundService
{
    private readonly ILogger<FileWriterService> _loger;
    private readonly LogQueue _logQueue;
    private readonly string _dataDirectory;
    private DateTime? _lastCleanupDay;
    private static readonly Encoding Utf8WithoutBom           = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions JsonOptions = new(){WriteIndented=false, Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};


    public FileWriterService(ILogger<FileWriterService> logger, LogQueue logQueue, IOptions<FileLogOptions> options, IHostEnvironment environment)
    {
        _loger           = logger;
        _logQueue        = logQueue;
        string directory = options.Value.Directory;
        if (string.IsNullOrWhiteSpace(directory)) { throw new Exception("FILE LOG DIRECTORY IS EMPTY");}
        _dataDirectory = Path.GetFullPath(directory, environment.ContentRootPath);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try {
            Directory.CreateDirectory(_dataDirectory);
            _loger.LogInformation("Writer is inited. Directory={Directory}", _dataDirectory);
            // we read until the queue is closes and emptied
            await foreach(Message message in _logQueue.Reader.ReadAllAsync())
            {
                await WriteMessageAsync(message);
                DeletePreviosYear(message.DateUtc);
            }
            _loger.LogInformation("Writer stoped. Queue emptied");
        
        } catch (Exception exception){
            // It also producers who are waiting for space
            _logQueue.Complete(exception);
            Environment.ExitCode = 1;
            _loger.LogCritical(exception, "The file writer has failed");
            throw;
        }
    }

    private async Task WriteMessageAsync(Message message)
    {
        string yearDirectory = Path.Combine(_dataDirectory, message.DateUtc.ToString("yyyy", CultureInfo.InstalledUICulture));
        Directory.CreateDirectory(yearDirectory);
        string filePath = Path.Combine(yearDirectory, message.DateUtc.ToString("MM", CultureInfo.InstalledUICulture)+".jsonl");
        var entry = new {
            date_utc   = message.DateUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            date_local = message.DateLocal.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            topic      = message.Topic,
            content    = ParseContent(message.Content)
        };
        string jsonLine = JsonSerializer.Serialize(entry, JsonOptions);
        await File.AppendAllTextAsync(filePath, jsonLine+"\n", Utf8WithoutBom);
    }

    private static object ParseContent(string content)
    {
        try {
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.Clone();
        } catch (Exception exception){
            return content.ToString();
        }
    }
    private void DeletePreviosYear(DateTime dateUtc)
    {
        if(_lastCleanupDay == dateUtc.Date){ return; }
        _lastCleanupDay = dateUtc.Date;
        string previosYearDirectory = Path.Combine(_dataDirectory, (dateUtc.Year - 1).ToString("D4", CultureInfo.InvariantCulture));
        try{
            if (Directory.Exists(previosYearDirectory)){
                Directory.Delete(previosYearDirectory, recursive: true);
            }
        } catch(Exception e){}
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logQueue.Complete();
        await base.StopAsync(cancellationToken);
        if(ExecuteTask is not null && !ExecuteTask.IsCompleted){
            Environment.ExitCode = 1;
        }
    }
}