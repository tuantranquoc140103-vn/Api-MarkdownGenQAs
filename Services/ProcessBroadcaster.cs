using System.Threading.Channels;

public class ProcessBroadcaster : IProcessBroadcaster
{
    // Tạo một kênh truyền dữ liệu (Unbounded = không giới hạn số lượng tin nhắn)
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public async ValueTask PublishAsync(string message)
    {
        await _channel.Writer.WriteAsync(message);
    }

    public IAsyncEnumerable<string> SubscribeAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}