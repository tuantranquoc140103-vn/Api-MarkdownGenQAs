using System.Diagnostics;
using System.Text;
using Hangfire;
using Hangfire.Server;
using MarkdownGenQAs.Interfaces;
using MarkdownGenQAs.Interfaces.Repository;
using MarkdownGenQAs.Models;
using MarkdownGenQAs.Models.DB;
using MarkdownGenQAs.Models.Enum;
using MarkdownGenQAs.Options;

public class BackgroundJobService : IBackgroundJobService
{
    private readonly ILogger<BackgroundJobService> _logger;
    private readonly IFileMetadataRepository _fileMetadataRepository;
    private readonly ILogMessageRepository _logMessageRepository;
    private readonly IMarkdownService _markdownService;
    private readonly IGenQAsService _genQAsService;
    private readonly IS3Service _s3Service;
    private readonly IJsonService _jsonService;
    private readonly IProcessBroadcaster _broadcaster;
    private readonly ICacheService _cacheService;

    public BackgroundJobService(
        ILogger<BackgroundJobService> logger,
        IFileMetadataRepository fileMetadataRepository,
        ILogMessageRepository logMessageRepository,
        IGenQAsService genQAsService,
        IMarkdownService markdownService,
        IS3Service s3Service,
        IJsonService jsonService,
        IProcessBroadcaster broadcaster,
        ICacheService cacheService)
    {
        _logger = logger;
        _fileMetadataRepository = fileMetadataRepository;
        _logMessageRepository = logMessageRepository;
        _genQAsService = genQAsService;
        _markdownService = markdownService;
        _s3Service = s3Service;
        _jsonService = jsonService;
        _broadcaster = broadcaster;
        _cacheService = cacheService;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ProcessFileMetadataAsync(Guid fileMetadataId, CancellationToken cancellationToken, PerformContext? context = null)
    {

        // Prevent stale jobs from re-processing after server restart
        if (context != null)
        {
            string cacheKey = $"job:{fileMetadataId}";
            var activeJobId = await _cacheService.GetAsync<string>(cacheKey);

            if (activeJobId != null && activeJobId != context.BackgroundJob.Id)
            {
                _logger.LogWarning("Job {CurrentJobId} is stale. Active Job ID for file {FileId} is {ActiveJobId}. Exiting.",
                    context.BackgroundJob.Id, fileMetadataId, activeJobId);
                return;
            }

            // If activeJobId is null, it means the job was either cleaned up in finally block or 
            // the server restarted before this check. In case of server restart, the finally block
            // of the previous run would have cleared it. 
            // If we want to be strict: if it's null, we also stop.
            if (activeJobId == null)
            {
                _logger.LogWarning("No active job record found for file {FileId} (possibly cleared on shutdown). Job {JobId} exiting.",
                    fileMetadataId, context.BackgroundJob.Id);
                return;
            }
        }

        _logger.LogInformation("Starting background processing for file metadata: {Id}", fileMetadataId);
        List<NotificationMessage> logMessages = new List<NotificationMessage>();

        async Task AddLogAndBroadcastAsync(string message, string? status = null)
        {
            var notification = new NotificationMessage
            {
                FileMetadataId = fileMetadataId,
                Message = message,
                Status = status ?? StatusFile.Processing.ToString()
            };
            logMessages.Add(notification);
            await _broadcaster.PublishAsync(notification);
        }

        try
        {
            await AddLogAndBroadcastAsync($"Starting background processing for file {fileMetadataId}");
            Stopwatch stopwatch = Stopwatch.StartNew();
            var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileMetadataId);
            cancellationToken.ThrowIfCancellationRequested();

            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata not found: {Id}", fileMetadataId);
                return;
            }

            fileMetadata.Status = StatusFile.Processing;
            _fileMetadataRepository.Update(fileMetadata);
            await _fileMetadataRepository.SaveChangesAsync();
            cancellationToken.ThrowIfCancellationRequested();

            string? markdownContent = await _s3Service.GetFileContentAsync(fileMetadata.ObjectKeyMarkdownOcr, S3Buckets.MarkdownOcr);
            if (string.IsNullOrEmpty(markdownContent)) return;
            cancellationToken.ThrowIfCancellationRequested();

            string objectKeyMarkdown = fileMetadata.ObjectKeyMarkdownOcr;

            // 1. Gen Summary document
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Summary generating file: {FileName}", fileMetadata.FileName);
            await AddLogAndBroadcastAsync($"Summary generating file: {fileMetadata.FileName}");

            var summaryDocument = await _genQAsService.GenSummaryDocumentAsync(markdownContent, fileMetadata.FileName);
            await AddLogAndBroadcastAsync($"Summary generated Success for file: {fileMetadata.FileName}");
            // 2. Gen chunks
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Chunking file: {FileName}", fileMetadata.FileName);
            await AddLogAndBroadcastAsync($"Chunking file: {fileMetadata.FileName}");

            var chunks = await _markdownService.CreateChunkDocument(markdownContent);
            await AddLogAndBroadcastAsync($"Chunking completed with {chunks.Count} chunks");

            // 3. Gen QAs summary
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("QAs summary generating file: {FileName}", fileMetadata.FileName);
            await AddLogAndBroadcastAsync($"QAs summary generating file: {fileMetadata.FileName}");

            var qaSummary = await _genQAsService.GenQAsSumaryAsync(markdownContent, fileMetadata.FileName);

            //4 .Gen QAs Text
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("QAs for chunks generating file: {FileName}", fileMetadata.FileName);
            await AddLogAndBroadcastAsync($"QAs for chunks generating file: {fileMetadata.FileName}");

            List<ChunkQAInfor> chunkQAInfors = new List<ChunkQAInfor>();
            // add QA type summary
            var chunkInfoSummary = new ChunkInfo { Type = TypeChunk.Summary, TokensCount = -1, Content = $"objectKey: {fileMetadata.ObjectKeyMarkdownOcr}" };
            chunkQAInfors.Add(new ChunkQAInfor { chunkInfo = chunkInfoSummary, QAs = qaSummary });
            int i = 1;
            foreach (var chunk in chunks)
            {
                _logger.LogInformation("QAs for file {fileName}, {index}/{TotalCount}", fileMetadata.FileName, i++, chunks.Count);

                if (chunk.Type == TypeChunk.Text)
                {
                    var textQAs = await _genQAsService.GenQAsTextAsync(chunk, summaryDocument, fileMetadata.FileName);
                    chunkQAInfors.Add(new ChunkQAInfor { chunkInfo = chunk, QAs = textQAs });
                }
                else if (chunk.Type == TypeChunk.Table)
                {
                    var tableQAs = await _genQAsService.GenQAsTableAsync(chunk, summaryDocument, fileMetadata.FileName);
                    chunkQAInfors.Add(new ChunkQAInfor { chunkInfo = chunk, QAs = tableQAs });
                }
                cancellationToken.ThrowIfCancellationRequested();
            }


            string jsonStringChunkQAs = _jsonService.Serialize(chunkQAInfors);

            // 5. Save file chunkQA, summary, chunkQAsSummary to S3
            _logger.LogInformation("Saving QAs Chunk: {FileName}", fileMetadata.FileName);
            await AddLogAndBroadcastAsync($"Saving QAs Chunk: {fileMetadata.FileName}");

            // save chunkQA
            string objectJsonChunkQAs = $"{objectKeyMarkdown}-chunkQA.json";

            byte[] byteArray = Encoding.UTF8.GetBytes(jsonStringChunkQAs);
            using (MemoryStream stream = new MemoryStream(byteArray))
            {
                objectJsonChunkQAs = await _s3Service.UploadFileAsync(stream, objectJsonChunkQAs, S3Buckets.ChunkQa, "application/json");
            }
            await AddLogAndBroadcastAsync($"Saved QAs Chunk: {fileMetadata.FileName}");

            // save summary
            await AddLogAndBroadcastAsync($"Saving summary for: {fileMetadata.FileName}");
            string objectKeySummary = $"{objectKeyMarkdown}-summary.txt";
            byte[] summaryBytes = Encoding.UTF8.GetBytes(summaryDocument);
            using (MemoryStream ms = new MemoryStream(summaryBytes))
            {
                objectKeySummary = await _s3Service.UploadFileAsync(ms, objectKeySummary, S3Buckets.DocumentSummary, "text/plain");
            }
            await AddLogAndBroadcastAsync($"Saved summary for: {fileMetadata.FileName}");

            await AddLogAndBroadcastAsync($"Total Chunks: {chunks.Count()}");
            await AddLogAndBroadcastAsync($"Completed processing file metadata: {fileMetadataId}", StatusFile.Successed.ToString());

            _logger.LogInformation("Completed processing file metadata: {Id}", fileMetadataId);
            _logger.LogInformation("Saving log messages for file metadata: {Id}", fileMetadataId);

            string logMessageStr = _jsonService.Serialize(logMessages);

            var existingLog = await _logMessageRepository.FirstOrDefaultAsync(l => l.FileMetadataId == fileMetadataId);
            if (existingLog != null)
            {
                existingLog.Message = logMessageStr;
                _logMessageRepository.Update(existingLog);
            }
            else
            {
                await _logMessageRepository.AddAsync(new LogMessage { Message = logMessageStr, FileMetadataId = fileMetadataId });
            }
            await _logMessageRepository.SaveChangesAsync();

            // 6. Update status Success and objectKey
            fileMetadata.Status = StatusFile.Successed;
            fileMetadata.ObjectKeyDocumentSummary = objectKeySummary;
            fileMetadata.ObjectKeyChunkQa = objectJsonChunkQAs;

            _fileMetadataRepository.Update(fileMetadata);
            await _fileMetadataRepository.SaveChangesAsync();



        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Processing was cancelled for file {Id}.", fileMetadataId);

            try
            {
                await AddLogAndBroadcastAsync($"Job was cancelled", StatusFile.Canceled.ToString());
            }
            catch { /* Ignore if services already disposed */ }

            try
            {
                var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileMetadataId);
                if (fileMetadata != null)
                {
                    fileMetadata.Status = StatusFile.Failed;
                    _fileMetadataRepository.Update(fileMetadata);
                    await _fileMetadataRepository.SaveChangesAsync();

                    string cancelLog = _jsonService.Serialize(logMessages);
                    var existingLog = await _logMessageRepository.FirstOrDefaultAsync(l => l.FileMetadataId == fileMetadataId);
                    if (existingLog != null)
                    {
                        existingLog.Message = cancelLog;
                        _logMessageRepository.Update(existingLog);
                    }
                    else
                    {
                        await _logMessageRepository.AddAsync(new LogMessage
                        {
                            Message = cancelLog,
                            FileMetadataId = fileMetadataId
                        });
                    }
                    await _logMessageRepository.SaveChangesAsync();
                }
            }
            catch { /* Ignore if DB connection already closed */ }

            // Do NOT re-throw to prevent Hangfire from automatically retrying the job
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file metadata: {Id}", fileMetadataId);
            await AddLogAndBroadcastAsync($"Error processing file: {ex.Message}", StatusFile.Failed.ToString());

            // Update status to failed
            try
            {
                var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileMetadataId);
                if (fileMetadata != null)
                {
                    fileMetadata.Status = StatusFile.Failed;
                    _fileMetadataRepository.Update(fileMetadata);
                    await _fileMetadataRepository.SaveChangesAsync();

                    // Upsert log message for failure
                    string errorLog = _jsonService.Serialize(logMessages);
                    var existingLog = await _logMessageRepository.FirstOrDefaultAsync(l => l.FileMetadataId == fileMetadataId);
                    if (existingLog != null)
                    {
                        existingLog.Message = errorLog;
                        _logMessageRepository.Update(existingLog);
                    }
                    else
                    {
                        await _logMessageRepository.AddAsync(new LogMessage
                        {
                            Message = errorLog,
                            FileMetadataId = fileMetadataId
                        });
                    }
                    await _logMessageRepository.SaveChangesAsync();
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update status to Failed for file metadata: {Id}", fileMetadataId);
            }

            throw;
        }
        finally
        {
            // Remove jobId from cache ONLY if it matches the current job ID
            // This prevents a cancelled job's cleanup from wiping out a NEW job's cache entry
            if (context != null)
            {
                string cacheKey = $"job:{fileMetadataId}";
                var cachedJobId = await _cacheService.GetAsync<string>(cacheKey);
                if (cachedJobId == context.BackgroundJob.Id)
                {
                    await _cacheService.RemoveAsync(cacheKey);
                }
            }
        }
    }

    public async Task CleanupOldLogsAsync()
    {
        _logger.LogInformation("Starting cleanup of old logs");

        try
        {
            // Get logs older than 30 days based on UpdatedAt from BaseEntity
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var oldLogs = await _logMessageRepository.FindAsync(log => log.UpdatedAt < cutoffDate);

            var count = oldLogs.Count();
            if (count == 0)
            {
                _logger.LogInformation("No old logs to cleanup");
                return;
            }

            // Delete old logs
            foreach (var log in oldLogs)
            {
                _logMessageRepository.Delete(log);
            }

            await _logMessageRepository.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} old log entries", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during log cleanup");
            throw;
        }
    }

    public async Task GenerateFileSummaryAsync(Guid fileMetadataId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting summary generation for file metadata: {Id}", fileMetadataId);

        try
        {
            var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileMetadataId);
            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata not found: {Id}", fileMetadataId);
                return;
            }

            // Update status
            cancellationToken.ThrowIfCancellationRequested();

            // Simulate summary generation
            _logger.LogInformation("Generating summary for: {FileName}", fileMetadata.FileName);
            await Task.Delay(3000); // Simulate work

            // In real implementation, you would:
            // 1. Get file content from S3
            // 2. Process with LLM service
            // 3. Save summary back to S3
            // 4. Update ObjectKeyDocumentSummary

            _logger.LogInformation("Completed summary generation for: {Id}", fileMetadataId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary for file metadata: {Id}", fileMetadataId);

            try
            {
                var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileMetadataId);
                if (fileMetadata != null)
                {
                    fileMetadata.Status = StatusFile.Failed;
                    _fileMetadataRepository.Update(fileMetadata);
                    await _fileMetadataRepository.SaveChangesAsync();
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update status to Failed for file metadata: {Id}", fileMetadataId);
            }

            throw;
        }
    }
}
