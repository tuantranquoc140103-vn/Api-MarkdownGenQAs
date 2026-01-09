using MarkdownGenQAs.Models;

public interface IProcessBroadcaster
{
    ValueTask PublishAsync(NotificationMessage message);
    IAsyncEnumerable<NotificationMessage> SubscribeAsync(Guid fileMetadataId, CancellationToken ct);
}