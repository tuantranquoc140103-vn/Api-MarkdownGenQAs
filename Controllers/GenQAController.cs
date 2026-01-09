using Hangfire;
using MarkdownGenQAs.Interfaces;
using MarkdownGenQAs.Interfaces.Repository;
using MarkdownGenQAs.Models;
using MarkdownGenQAs.Models.Enum;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace MarkdownGenQAs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class GenQAController : ControllerBase
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IFileMetadataRepository _fileMetadataRepository;
    private readonly ICacheService _cacheService;
    private readonly IProcessBroadcaster _broadcaster;
    private readonly ILogger<GenQAController> _logger;
    private readonly IJsonService _jsonService;

    public GenQAController(
        IBackgroundJobClient backgroundJobClient,
        IFileMetadataRepository fileMetadataRepository,
        ICacheService cacheService,
        IProcessBroadcaster broadcaster,
        ILogger<GenQAController> logger,
        IJsonService jsonService)
    {
        _backgroundJobClient = backgroundJobClient;
        _fileMetadataRepository = fileMetadataRepository;
        _cacheService = cacheService;
        _broadcaster = broadcaster;
        _logger = logger;
        _jsonService = jsonService;
    }

    /// <summary>
    /// Trigger background QA generation for a file
    /// </summary>
    /// <param name="fileMetadataId">The ID of the file metadata record</param>
    /// <returns>Job ID of the enqueued background job</returns>
    [HttpPost("process/{fileMetadataId}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessFile(Guid fileMetadataId)
    {
        try
        {
            _logger.LogInformation("Request to process file metadata: {Id}", fileMetadataId);

            var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileMetadataId);
            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata with ID {Id} not found", fileMetadataId);
                return NotFound($"File metadata with ID {fileMetadataId} not found");
            }

            // Enqueue the job
            // Hangfire's IBackgroundJobClient.Enqueue handles the CancellationToken injection automatically if the method signature includes it.
            var jobId = _backgroundJobClient.Enqueue<IBackgroundJobService>(
                service => service.ProcessFileMetadataAsync(fileMetadataId, CancellationToken.None, null));

            // Cache the job ID in Redis
            // Use a key pattern for the specific file metadata - TTL set to 3 hours as requested
            string cacheKey = $"job:{fileMetadataId}";
            await _cacheService.SetAsync(cacheKey, jobId, TimeSpan.FromHours(3));

            _logger.LogInformation("Successfully enqueued processing job {JobId} for file {Id}", jobId, fileMetadataId);

            return Accepted(new { JobId = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing processing job for file {Id}", fileMetadataId);
            return StatusCode(500, "An error occurred while starting the background process");
        }
    }

    /// <summary>
    /// Subscribe to real-time processing notifications for a file via SSE
    /// </summary>
    /// <param name="fileMetadataId">The ID of the file metadata record</param>
    [HttpGet("notifications/{fileMetadataId}")]
    public async Task GetNotifications(Guid fileMetadataId)
    {
        // 1. Check if a job exists for this file in Redis
        string cacheKey = $"job:{fileMetadataId}";
        _logger.LogInformation("Checking Redis with Key: {Key}", cacheKey);
        var jobId = await _cacheService.GetAsync<string>(cacheKey);
        _logger.LogInformation("Value found in Redis: {Value}", jobId ?? "NULL");
        if (string.IsNullOrEmpty(jobId))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // 2. Set up SSE response
        Response.Headers[HeaderNames.ContentType] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        var firstNoti = new NotificationMessage
        {
            FileMetadataId = fileMetadataId,
            Message = "Started processing...",
            Status = StatusFile.Processing.ToString()
        };
        // SSE format: data: {json_object}\n\n
        // Use compact serialization (no indentation) for SSE compatibility

        string dataJson = System.Text.Json.JsonSerializer.Serialize(firstNoti, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        await Response.WriteAsync($"data: {dataJson}\n\n");
        await Response.Body.FlushAsync();
        // 3. Listen for notifications and stream them
        try
        {
            await foreach (var notification in _broadcaster.SubscribeAsync(fileMetadataId, HttpContext.RequestAborted))
            {
                // SSE format: data: {json_object}\n\n
                // Use compact serialization (no indentation) for SSE compatibility
                string jsonNotification = System.Text.Json.JsonSerializer.Serialize(notification, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                await Response.WriteAsync($"data: {jsonNotification}\n\n");
                await Response.Body.FlushAsync();

                // If job is finished (Success or Failed), we can stop streaming
                if (notification.Message.Contains("Successed", StringComparison.OrdinalIgnoreCase)
                    || notification.Message.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client closed the connection
            _logger.LogInformation("SSE connection closed for file {Id}", fileMetadataId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming notifications for file {Id}", fileMetadataId);
        }
    }

    /// <summary>
    /// Cancel a running background job for a file
    /// </summary>
    /// <param name="fileMetadataId">The ID of the file metadata record</param>
    /// <returns>Success status</returns>
    [HttpPost("cancel/{fileMetadataId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelProcess(Guid fileMetadataId)
    {
        try
        {
            _logger.LogInformation("Request to cancel job for file metadata: {Id}", fileMetadataId);

            // 1. Get Job ID from Redis
            string cacheKey = $"job:{fileMetadataId}";
            var jobId = await _cacheService.GetAsync<string>(cacheKey);

            if (string.IsNullOrEmpty(jobId))
            {
                _logger.LogWarning("No active job found in cache for file {Id}", fileMetadataId);
                return NotFound("No active background job found for this file");
            }

            // 2. Update status to Failed
            var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileMetadataId);
            if (fileMetadata != null)
            {
                fileMetadata.Status = Models.Enum.StatusFile.Failed;
                _fileMetadataRepository.Update(fileMetadata);
                await _fileMetadataRepository.SaveChangesAsync();
            }

            // 3. Tell Hangfire to delete/cancel the job
            // This will trigger the CancellationToken in the background job
            bool deleted = _backgroundJobClient.Delete(jobId);

            if (deleted)
            {
                _logger.LogInformation("Successfully requested cancellation for Job {JobId}", jobId);
                return Ok(new { Message = "Cancellation requested successfully", JobId = jobId });
            }
            else
            {
                _logger.LogWarning("Hangfire could not delete Job {JobId}. It might already be finished.", jobId);
                return Ok(new { Message = "Job might have already finished or could not be cancelled", JobId = jobId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling job for file {Id}", fileMetadataId);
            return StatusCode(500, "An error occurred while attempting to cancel the job");
        }
    }
}
