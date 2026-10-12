using System.Threading.Channels;
namespace MqttNetService.Database;

public sealed record DbMessage(Message message);
public sealed class DbQueue{
    private readonly Channel<DbMessage> _channel = Channel.CreateBounded<DbMessage>(new BoundedChannelOptions(11111){
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });
    public ChannelReader<DbMessage> Reader => _channel.Reader;
    public ValueTask EnqueueAsync(Message message){
        var item = new DbMessage(message);
        return _channel.Writer.WriteAsync(item);
    }
    public void Complete(Exception? error = null){
        _channel.Writer.TryComplete(error);
    }
}