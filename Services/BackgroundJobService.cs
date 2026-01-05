using System.Diagnostics;
using System.Text;
using Hangfire;
using MarkdownGenQAs.Interfaces.Repository;
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

    public BackgroundJobService(
        ILogger<BackgroundJobService> logger,
        IFileMetadataRepository fileMetadataRepository,
        ILogMessageRepository logMessageRepository,
        IGenQAsService genQAsService,
        IMarkdownService markdownService,
        IS3Service s3Service,
        IJsonService jsonService)
    {
        _logger = logger;
        _fileMetadataRepository = fileMetadataRepository;
        _logMessageRepository = logMessageRepository;
        _genQAsService = genQAsService;
        _markdownService = markdownService;
        _s3Service = s3Service;
        _jsonService = jsonService;
    }

    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ProcessFileMetadataAsync(Guid fileMetadataId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting background processing for file metadata: {Id}", fileMetadataId);
        StringBuilder logMessage = new StringBuilder();
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileMetadataId);
            cancellationToken.ThrowIfCancellationRequested();
            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata not found: {Id}", fileMetadataId);
                return;
            }

            string? markdownContent = await _s3Service.GetFileContentAsync(fileMetadata.ObjectKeyMarkdownOcr, S3Buckets.MarkdownOcr);
            if (string.IsNullOrEmpty(markdownContent)) return;

            string objectKeyMarkdown = fileMetadata.ObjectKeyMarkdownOcr;

            // 1. Gen Summary document
            fileMetadata.Status = StatusFile.SummaryGenerating;
            _fileMetadataRepository.Update(fileMetadata);
            await _fileMetadataRepository.SaveChangesAsync();
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Summary generating file: {FileName}", fileMetadata.FileName);
            logMessage.Append($"[{DateTime.UtcNow.ToString("HH:mm:ss")}] - Summary generating file: {fileMetadata.FileName}");

            var summaryDocument = await _genQAsService.GenSummaryDocumentAsync(markdownContent, fileMetadata.FileName);
            string objectKeySummary = $"{objectKeyMarkdown}-summary.txt";
            byte[] summaryBytes = Encoding.UTF8.GetBytes(summaryDocument);
            using (MemoryStream ms = new MemoryStream(summaryBytes))
            {
                await _s3Service.UploadFileAsync(ms, objectKeySummary, S3Buckets.DocumentSummary);
            }

            // 2. Gen chunks
            fileMetadata.Status = StatusFile.Chunking;
            _fileMetadataRepository.Update(fileMetadata);
            await _fileMetadataRepository.SaveChangesAsync();
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Chunking file: {FileName}", fileMetadata.FileName);
            logMessage.Append($"[{DateTime.UtcNow.ToString("HH:mm:ss")}] - Chunking file: {fileMetadata.FileName}");

            var chunks = await _markdownService.CreateChunkDocument(markdownContent);

            // 3. Gen QAs summary
            fileMetadata.Status = StatusFile.QASummaryGenerating;
            _fileMetadataRepository.Update(fileMetadata);
            await _fileMetadataRepository.SaveChangesAsync();
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("QAs summary generating file: {FileName}", fileMetadata.FileName);
            logMessage.Append($"[{DateTime.UtcNow.ToString("HH:mm:ss")}] - QAs summary generating file: {fileMetadata.FileName}");

            var qaSummary = await _genQAsService.GenQAsSumaryAsync(markdownContent, fileMetadata.FileName);

            //4 .Gen QAs Text
            fileMetadata.Status = StatusFile.QAGenerating;
            _fileMetadataRepository.Update(fileMetadata);
            await _fileMetadataRepository.SaveChangesAsync();
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("QAs generating file: {FileName}", fileMetadata.FileName);
            logMessage.Append($"[{DateTime.UtcNow.ToString("HH:mm:ss")}] - QAs generating file: {fileMetadata.FileName}");

            List<TextQAInfor> textQAInfors = new List<TextQAInfor>();
            List<TableQAInfor> tableQAInfors = new List<TableQAInfor>();
            foreach (var chunk in chunks)
            {
                if (chunk.Type == TypeChunk.Text)
                {
                    var textQAs = await _genQAsService.GenQAsTextAsync(chunk, summaryDocument, fileMetadata.FileName);
                    textQAInfors.Add(new TextQAInfor { chunkInfo = chunk, QAs = textQAs });
                }
                else if (chunk.Type == TypeChunk.Table)
                {
                    var tableQAs = await _genQAsService.GenQAsTableAsync(chunk, summaryDocument, fileMetadata.FileName);
                    tableQAInfors.Add(new TableQAInfor { chunkInfo = chunk, QAs = tableQAs });
                }
                cancellationToken.ThrowIfCancellationRequested();
            }

            var chunkQAText = textQAInfors.Select(t =>
            {
                var qas = t.QAs.Select(q => new ChunkQA { Question = q.Question, Answer = q.Answer, Category = q.Category.ToString() }).ToList();
                return new ChunkQAInfor
                {
                    chunkInfo = t.chunkInfo,
                    QAs = qas
                };
            }).ToList();

            var chunkQATable = tableQAInfors.Select(t =>
            {
                var qas = t.QAs.Select(q => new ChunkQA { Question = q.Question, Answer = q.Answer, Category = q.category.ToString() }).ToList();
                return new ChunkQAInfor
                {
                    chunkInfo = t.chunkInfo,
                    QAs = qas
                };
            }).ToList();

            var chunkQAs = chunkQAText.Concat(chunkQATable);

            string jsonStringChunkQAs = _jsonService.Serialize(chunkQAs);

            // 5. Save file chunkQA to S3
            _logger.LogInformation("Saving QAs Chunk: {FileName}", fileMetadata.FileName);
            logMessage.Append($"[{DateTime.UtcNow.ToString("HH:mm:ss")}] - Saving QAs Chunk: {fileMetadata.FileName}");

            string objectJsonChunkQAs = $"{objectKeyMarkdown}-chunkQA.json";

            byte[] byteArray = Encoding.UTF8.GetBytes(jsonStringChunkQAs);
            using (MemoryStream stream = new MemoryStream(byteArray))
            {
                await _s3Service.UploadFileAsync(stream, objectJsonChunkQAs, S3Buckets.ChunkQa, "application/json");
            }

            logMessage.Append($"[{DateTime.UtcNow.ToString("HH:mm:ss")}] - Completed processing file: {fileMetadata.FileName}");
            logMessage.Append($"[{DateTime.UtcNow.ToString("HH:mm:ss")}] - Total Chunks: {chunks.Count()}");
            int totalQAs = chunkQAs.SelectMany(c => c.QAs).Count();
            logMessage.Append($"[{DateTime.UtcNow.ToString("HH:mm:ss")}] - Total QAs: {totalQAs}");

            string logMessageStr = logMessage.ToString();

            await _logMessageRepository.AddAsync(new LogMessage { Message = logMessageStr, FileMetadataId = fileMetadataId });

            // 6. Update status Success
            fileMetadata.Status = StatusFile.Successed;
            _fileMetadataRepository.Update(fileMetadata);
            await _fileMetadataRepository.SaveChangesAsync();


        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file metadata: {Id}", fileMetadataId);
            logMessage.Append($"[{DateTime.UtcNow.ToString("HH:mm:ss")}] - Error processing file: {ex.Message}");

            // Update status to failed
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
            fileMetadata.Status = StatusFile.SummaryGenerating;
            _fileMetadataRepository.Update(fileMetadata);
            await _fileMetadataRepository.SaveChangesAsync();
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
