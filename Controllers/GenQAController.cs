using Hangfire;
using MarkdownGenQAs.Interfaces.Repository;
using Microsoft.AspNetCore.Mvc;

namespace MarkdownGenQAs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class GenQAController : ControllerBase
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IFileMetadataRepository _fileMetadataRepository;
    private readonly ILogger<GenQAController> _logger;

    public GenQAController(
        IBackgroundJobClient backgroundJobClient,
        IFileMetadataRepository fileMetadataRepository,
        ILogger<GenQAController> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _fileMetadataRepository = fileMetadataRepository;
        _logger = logger;
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
                service => service.ProcessFileMetadataAsync(fileMetadataId, CancellationToken.None));

            _logger.LogInformation("Successfully enqueued processing job {JobId} for file {Id}", jobId, fileMetadataId);

            return Accepted(new { JobId = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing processing job for file {Id}", fileMetadataId);
            return StatusCode(500, "An error occurred while starting the background process");
        }
    }
}
