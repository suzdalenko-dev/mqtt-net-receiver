using System.Threading.Channels;
using Microsoft.Extensions.Options;
namespace MqttNetService.Files;

public sealed class LogQueue
{
    private readonly Channel<Message> _channel;
    public LogQueue(IOptions<FileLogOptions> options)
    {
        _channel = Channel.CreateBounded<Message>(new BoundedChannelOptions(options.Value.QueueCapacity){
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false
            }
        );
    }
    public ChannelReader<Message> Reader => _channel.Reader;
    public ValueTask EnqueueAsync(Message message)
    {
        return _channel.Writer.WriteAsync(message);
    }
    public void Complete(Exception? error = null)
    {
        _channel.Writer.TryComplete(error);
    }
}