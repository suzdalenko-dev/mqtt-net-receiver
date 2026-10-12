using System.Threading.Channels;
namespace MqttNetService.Database;

public sealed class DbQueue{
    private readonly Channel<Message> _channel = Channel.CreateBounded<Message>(new BoundedChannelOptions(11111){
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });
    public ChannelReader<Message> Reader => _channel.Reader;
    public ValueTask EnqueueAsync(Message message, CancellationToken cancellationToken = default){
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }
    public void Complete(Exception? error = null){
        _channel.Writer.TryComplete(error);
    }
}
