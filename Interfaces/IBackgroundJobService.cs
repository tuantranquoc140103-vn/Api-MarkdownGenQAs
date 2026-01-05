public interface IBackgroundJobService
{
    Task ProcessFileMetadataAsync(Guid fileMetadataId, CancellationToken cancellationToken);
    Task CleanupOldLogsAsync();
    Task GenerateFileSummaryAsync(Guid fileMetadataId, CancellationToken cancellationToken);
}
