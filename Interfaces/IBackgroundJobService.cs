using Hangfire.Server;

public interface IBackgroundJobService
{
    Task ProcessFileMetadataAsync(Guid fileMetadataId, CancellationToken cancellationToken, PerformContext? context = null);
    Task CleanupOldLogsAsync();
    Task GenerateFileSummaryAsync(Guid fileMetadataId, CancellationToken cancellationToken);
}
