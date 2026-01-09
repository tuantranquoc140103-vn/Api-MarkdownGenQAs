using MarkdownGenQAs.Interfaces.Repository;
using MarkdownGenQAs.Models.DB;
using Microsoft.AspNetCore.Mvc;

namespace MarkdownGenQAs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class LogMessageController : ControllerBase
{
    private readonly ILogMessageRepository _logMessageRepository;
    private readonly ILogger<LogMessageController> _logger;

    public LogMessageController(ILogMessageRepository logMessageRepository, ILogger<LogMessageController> logger)
    {
        _logMessageRepository = logMessageRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get log message for a specific file metadata
    /// </summary>
    /// <param name="fileMetadataId">The ID of the file metadata</param>
    /// <returns>The log message object</returns>
    [HttpGet("file/{fileMetadataId}")]
    [ProducesResponseType(typeof(LogMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByFileId(Guid fileMetadataId)
    {
        try
        {
            var logMessage = await _logMessageRepository.FirstOrDefaultAsync(l => l.FileMetadataId == fileMetadataId);

            if (logMessage == null)
            {
                return NotFound($"No log message found for file metadata ID {fileMetadataId}");
            }

            return Ok(logMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving log message for file {Id}", fileMetadataId);
            return StatusCode(500, "An error occurred while retrieving the log message");
        }
    }
}
