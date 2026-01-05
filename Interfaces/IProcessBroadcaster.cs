public interface IProcessBroadcaster
{
    ValueTask PublishAsync(string message);
    IAsyncEnumerable<string> SubscribeAsync(CancellationToken ct);
}