using System.Runtime.CompilerServices;
using MarkdownGenQAs.Interfaces;
using MarkdownGenQAs.Models;
using StackExchange.Redis;

namespace MarkdownGenQAs.Services;

public class ProcessBroadcaster : IProcessBroadcaster
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IJsonService _jsonService;

    public ProcessBroadcaster(IConnectionMultiplexer redis, IJsonService jsonService)
    {
        _redis = redis;
        _jsonService = jsonService;
    }

    private RedisChannel GetChannel(Guid fileMetadataId) => RedisChannel.Literal($"process_notifications:{fileMetadataId}");

    public async ValueTask PublishAsync(NotificationMessage message)
    {
        var subscriber = _redis.GetSubscriber();
        var payload = _jsonService.Serialize(message);
        var channel = GetChannel(message.FileMetadataId);
        await subscriber.PublishAsync(channel, payload);
    }

    public async IAsyncEnumerable<NotificationMessage> SubscribeAsync(Guid fileMetadataId, [EnumeratorCancellation] CancellationToken ct)
    {
        var subscriber = _redis.GetSubscriber();
        var channelName = GetChannel(fileMetadataId);
        var channel = await subscriber.SubscribeAsync(channelName);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait for a message with a timeout to allow checking CancellationToken
                var msg = await channel.ReadAsync(ct);
                if (msg.Message.HasValue)
                {
                    var notification = _jsonService.Deserialize<NotificationMessage>(msg.Message!);
                    if (notification != null)
                    {
                        yield return notification;
                    }
                }
            }
        }
        finally
        {
            await channel.UnsubscribeAsync();
        }
    }
}