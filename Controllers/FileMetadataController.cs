using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MarkdownGenQAs.Models.Dto;
using MarkdownGenQAs.Interfaces.Repository;
using MarkdownGenQAs.Options;
using MarkdownGenQAs.Models.Enum;
using System.Text;
// using MarkdownGenQAs.Models.QA;

namespace MarkdownGenQAs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class FileMetadataController : ControllerBase
{
    private readonly IFileMetadataRepository _repository;
    private readonly ICategoryFileRepository _categoryRepository;
    private readonly IS3Service _s3Service;
    private readonly IJsonService _jsonService;
    private readonly ILogger<FileMetadataController> _logger;
    private const long MaxFileSize = 104857600; // 100MB

    public FileMetadataController(
        IFileMetadataRepository repository,
        ICategoryFileRepository categoryRepository,
        IS3Service s3Service,
        IJsonService jsonService,
        ILogger<FileMetadataController> logger)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _s3Service = s3Service;
        _jsonService = jsonService;
        _logger = logger;
    }

    /// <summary>
    /// Upload file to S3 and create metadata
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<ActionResult<FileMetadataDto>> Upload([FromForm] UploadFileMetadataDto dto)
    {
        try
        {
            // Validate file extension and content type
            var allowedContentTypes = new[] { "text/markdown", "text/plain", "application/octet-stream" };
            var extension = Path.GetExtension(dto.File.FileName).ToLowerInvariant();

            if (extension != ".md")
            {
                _logger.LogWarning("Invalid file extension: {Extension}", extension);
                return BadRequest("Chỉ chấp nhận file có định dạng .md");
            }

            if (!allowedContentTypes.Contains(dto.File.ContentType?.ToLower()))
            {
                _logger.LogWarning("Invalid content type: {ContentType}", dto.File.ContentType);
                return BadRequest("Định dạng Content-Type không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for file upload");
                return BadRequest(ModelState);
            }

            // Validate file size
            if (dto.File.Length == 0)
            {
                return BadRequest("File is empty");
            }

            if (dto.File.Length > MaxFileSize)
            {
                return BadRequest($"File size exceeds maximum limit of {MaxFileSize / 1024 / 1024}MB");
            }

            var fileName = dto.File.FileName;

            // Check if filename already exists in database
            var existingFile = await _repository.FindAsync(f => f.FileName == fileName);
            if (existingFile.Any())
            {
                _logger.LogWarning("File with name {FileName} already exists in database", fileName);
                return BadRequest($"File với tên '{fileName}' đã tồn tại. Mỗi file chỉ được upload 1 lần.");
            }

            // Check if file already exists in S3
            var fileExistsInS3 = await _s3Service.FileExistsAsync(fileName, S3Buckets.MarkdownOcr);
            if (fileExistsInS3)
            {
                _logger.LogWarning("File {FileName} already exists in S3 bucket {Bucket}", fileName, S3Buckets.MarkdownOcr);
                return BadRequest($"File '{fileName}' đã tồn tại trong storage. Vui lòng đổi tên file hoặc xóa file cũ.");
            }

            // Validate category if provided
            if (dto.CategoryId.HasValue)
            {
                var category = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value);
                if (category == null)
                {
                    _logger.LogWarning("Category with ID {CategoryId} not found", dto.CategoryId.Value);
                    return BadRequest($"Category with ID {dto.CategoryId.Value} not found");
                }
            }

            _logger.LogInformation("Uploading file {FileName} with size {FileSize} bytes", fileName, dto.File.Length);

            // Upload to S3 using fileName as objectKey
            string uploadedKey;
            using (var stream = dto.File.OpenReadStream())
            {
                uploadedKey = await _s3Service.UploadFileAsync(
                    stream,
                    fileName, // Use original fileName as objectKey
                    S3Buckets.MarkdownOcr,
                    dto.File.ContentType ?? "application/octet-stream"
                );
            }

            // Create metadata record
            var fileMetadata = new FileMetadata
            {
                FileName = fileName,
                FileType = dto.File.ContentType ?? "application/octet-stream",
                ObjectKeyMarkdownOcr = uploadedKey, // Will be same as fileName
                CategoryId = dto.CategoryId,
                Author = dto.Author,
                Status = StatusFile.Uploaded,
                ProcessingTime = 0
            };

            await _repository.AddAsync(fileMetadata);
            await _repository.SaveChangesAsync();

            var resultDto = new FileMetadataDto
            {
                Id = fileMetadata.Id,
                FileName = fileMetadata.FileName,
                FileType = fileMetadata.FileType,
                Status = fileMetadata.Status.ToString(),
                ObjectKeyMarkdownOcr = fileMetadata.ObjectKeyMarkdownOcr,
                ObjectKeyDocumentSummary = fileMetadata.ObjectKeyDocumentSummary,
                ObjectKeyChunkQa = fileMetadata.ObjectKeyChunkQa,
                ProcessingTime = fileMetadata.ProcessingTime,
                Author = fileMetadata.Author,
                CategoryId = fileMetadata.CategoryId,
                CreatedAt = fileMetadata.CreatedAt,
                UpdatedAt = fileMetadata.UpdatedAt
            };

            _logger.LogInformation("Successfully uploaded file and created metadata with ID: {Id}", fileMetadata.Id);
            return CreatedAtAction(nameof(GetById), new { id = fileMetadata.Id }, resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while uploading file");
            return StatusCode(500, "An error occurred while uploading the file");
        }
    }

    /// <summary>
    /// Get all file metadata
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FileMetadataDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FileMetadataDto>>> GetAll()
    {
        try
        {
            _logger.LogInformation("Getting all file metadata");
            var fileMetadatas = await _repository.GetAllAsync();

            var dtos = fileMetadatas.Select(fm => new FileMetadataDto
            {
                Id = fm.Id,
                FileName = fm.FileName,
                FileType = fm.FileType.ToString(),
                Status = fm.Status.ToString(),
                ObjectKeyMarkdownOcr = fm.ObjectKeyMarkdownOcr,
                ObjectKeyDocumentSummary = fm.ObjectKeyDocumentSummary,
                ObjectKeyChunkQa = fm.ObjectKeyChunkQa,
                ProcessingTime = fm.ProcessingTime,
                Author = fm.Author,
                CategoryId = fm.CategoryId,
                CategoryName = fm.CategoryFile?.Name,
                CreatedAt = fm.CreatedAt,
                UpdatedAt = fm.UpdatedAt
            });

            _logger.LogInformation("Successfully retrieved {Count} file metadata records", fileMetadatas.Count());
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting all file metadata");
            return StatusCode(500, "An error occurred while retrieving file metadata");
        }
    }

    /// <summary>
    /// Get file metadata by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FileMetadataDto>> GetById(Guid id)
    {
        try
        {
            _logger.LogInformation("Getting file metadata with ID: {Id}", id);
            var fileMetadata = await _repository.GetByIdAsync(id);

            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata with ID {Id} not found", id);
                return NotFound($"File metadata with ID {id} not found");
            }

            var dto = new FileMetadataDto
            {
                Id = fileMetadata.Id,
                FileName = fileMetadata.FileName,
                FileType = fileMetadata.FileType.ToString(),
                Status = fileMetadata.Status.ToString(),
                ObjectKeyMarkdownOcr = fileMetadata.ObjectKeyMarkdownOcr,
                ObjectKeyDocumentSummary = fileMetadata.ObjectKeyDocumentSummary,
                ObjectKeyChunkQa = fileMetadata.ObjectKeyChunkQa,
                ProcessingTime = fileMetadata.ProcessingTime,
                Author = fileMetadata.Author,
                CategoryId = fileMetadata.CategoryId,
                CategoryName = fileMetadata.CategoryFile?.Name,
                CreatedAt = fileMetadata.CreatedAt,
                UpdatedAt = fileMetadata.UpdatedAt
            };

            _logger.LogInformation("Successfully retrieved file metadata with ID: {Id}", id);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting file metadata with ID: {Id}", id);
            return StatusCode(500, "An error occurred while retrieving file metadata");
        }
    }



    /// <summary>
    /// Download QAs as a Markdown file
    /// </summary>
    [HttpGet("{id:guid}/download-qas-markdown")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadQAsMarkdown(Guid id)
    {
        try
        {
            var fileMetadata = await _repository.GetByIdAsync(id);

            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata with ID {Id} not found", id);
                return NotFound($"File metadata with ID {id} not found");
            }

            if (string.IsNullOrEmpty(fileMetadata.ObjectKeyChunkQa))
            {
                _logger.LogWarning("No QAs generated for file metadata ID: {Id}", id);
                return NotFound("No QAs have been generated for this file yet.");
            }
            if (string.IsNullOrEmpty(fileMetadata.ObjectKeyDocumentSummary))
            {
                _logger.LogWarning("No QAs generated for file metadata ID: {Id}", id);
                return NotFound("No QAs have been generated for this file yet.");
            }

            _logger.LogInformation($"download with object key: {fileMetadata.ObjectKeyChunkQa}");

            // 1. Download JSON from S3
            var jsonContent = await _s3Service.GetFileContentAsync(fileMetadata.ObjectKeyChunkQa, S3Buckets.ChunkQa);
            if (string.IsNullOrEmpty(jsonContent))
            {
                _logger.LogError("QA JSON content is empty for object key: {ObjectKey}", fileMetadata.ObjectKeyChunkQa);
                return NotFound("QA file content is empty.");
            }
            var summaryContent = await _s3Service.GetFileContentAsync(
                fileMetadata.ObjectKeyDocumentSummary,
                S3Buckets.DocumentSummary
            );

            if (string.IsNullOrEmpty(summaryContent))
            {
                _logger.LogError("File not found in S3 for object key: {ObjectKey}", fileMetadata.ObjectKeyDocumentSummary);
                return NotFound("File Summary not found in storage");
            }

            // 2. Deserialize JSON
            var chunkQAInfors = _jsonService.Deserialize<List<ChunkQAInfor>>(jsonContent);
            if (chunkQAInfors == null || !chunkQAInfors.Any())
            {
                _logger.LogWarning("No QA data found in JSON for metadata ID: {Id}", id);
                return NotFound("No QA data found for this file.");
            }

            // 3. Convert to Markdown
            var sb = new StringBuilder();
            
            sb.AppendLine($"# Document Summary: {summaryContent}");
            sb.AppendLine();
            int questionCounter = 1;

            foreach (var chunk in chunkQAInfors)
            {
                foreach (var qa in chunk.QAs)
                {
                    sb.AppendLine($"# Question [number {questionCounter}]: {qa.Question}");
                    sb.AppendLine($"## Answer: {qa.Answer}");
                    sb.AppendLine();
                    questionCounter++;
                }
            }

            // 4. Return as file
            var markdownContent = sb.ToString();
            var byteArray = Encoding.UTF8.GetBytes(markdownContent);
            var stream = new MemoryStream(byteArray);

            var downloadFileName = $"{Path.GetFileNameWithoutExtension(fileMetadata.FileName)}-QAs.md";
            _logger.LogInformation("Successfully generated QA Markdown for metadata ID: {Id}. Exporting as {FileName}", id, downloadFileName);

            return File(stream, "text/markdown", downloadFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while exporting QA Markdown for metadata ID: {Id}", id);
            return StatusCode(500, "An error occurred while exporting QA Markdown");
        }
    }

    /// <summary>
    /// Download file from S3
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id)
    {
        try
        {
            _logger.LogInformation("Downloading file for metadata ID: {Id}", id);
            var fileMetadata = await _repository.GetByIdAsync(id);

            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata with ID {Id} not found", id);
                return NotFound($"File metadata with ID {id} not found");
            }

            var stream = await _s3Service.DownloadFileAsync(
                fileMetadata.ObjectKeyMarkdownOcr,
                S3Buckets.MarkdownOcr
            );

            if (stream == null)
            {
                _logger.LogError("File not found in S3 for object key: {ObjectKey}", fileMetadata.ObjectKeyMarkdownOcr);
                return NotFound("File not found in storage");
            }

            _logger.LogInformation("Successfully retrieved file from S3 for metadata ID: {Id}", id);
            return File(stream, fileMetadata.FileType, fileMetadata.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while downloading file for metadata ID: {Id}", id);
            return StatusCode(500, "An error occurred while downloading the file");
        }
    }

    /// <summary>
    /// Download raw QA JSON from S3
    /// </summary>
    [HttpGet("{id:guid}/download-qas-json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadQAsJson(Guid id)
    {
        try
        {
            _logger.LogInformation("Downloading QA JSON for metadata ID: {Id}", id);
            var fileMetadata = await _repository.GetByIdAsync(id);

            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata with ID {Id} not found", id);
                return NotFound($"File metadata with ID {id} not found");
            }

            if (string.IsNullOrEmpty(fileMetadata.ObjectKeyChunkQa))
            {
                _logger.LogWarning("No QAs generated for file metadata ID: {Id}", id);
                return NotFound("No QAs have been generated for this file yet.");
            }

            var stream = await _s3Service.DownloadFileAsync(
                fileMetadata.ObjectKeyChunkQa,
                S3Buckets.ChunkQa
            );

            if (stream == null)
            {
                _logger.LogError("File not found in S3 for object key: {ObjectKey}", fileMetadata.ObjectKeyChunkQa);
                return NotFound("File chunkQA not found in storage");
            }

            var downloadFileName = $"{Path.GetFileNameWithoutExtension(fileMetadata.FileName)}-QAs.json";
            _logger.LogInformation("Successfully retrieved QA JSON from S3 for metadata ID: {Id}", id);
            return File(stream, "application/json", downloadFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while downloading QA JSON for metadata ID: {Id}", id);
            return StatusCode(500, "An error occurred while downloading the QA JSON");
        }
    }

    /// <summary>
    /// Update file metadata
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FileMetadataDto>> Update(Guid id, [FromBody] UpdateFileMetadataDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for updating file metadata with ID: {Id}", id);
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Updating file metadata with ID: {Id}", id);

            var fileMetadata = await _repository.GetByIdAsync(id);
            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata with ID {Id} not found", id);
                return NotFound($"File metadata with ID {id} not found");
            }

            // Validate category if provided
            if (dto.CategoryId.HasValue)
            {
                var category = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value);
                if (category == null)
                {
                    _logger.LogWarning("Category with ID {CategoryId} not found", dto.CategoryId.Value);
                    return BadRequest($"Category with ID {dto.CategoryId.Value} not found");
                }
            }

            // Update fields
            if (dto.Author != null)
                fileMetadata.Author = dto.Author;

            if (dto.CategoryId.HasValue)
                fileMetadata.CategoryId = dto.CategoryId;

            if (dto.Status.HasValue)
                fileMetadata.Status = dto.Status.Value;

            _repository.Update(fileMetadata);
            await _repository.SaveChangesAsync();

            var resultDto = new FileMetadataDto
            {
                Id = fileMetadata.Id,
                FileName = fileMetadata.FileName,
                FileType = fileMetadata.FileType.ToString(),
                Status = fileMetadata.Status.ToString(),
                ObjectKeyMarkdownOcr = fileMetadata.ObjectKeyMarkdownOcr,
                ObjectKeyDocumentSummary = fileMetadata.ObjectKeyDocumentSummary,
                ObjectKeyChunkQa = fileMetadata.ObjectKeyChunkQa,
                ProcessingTime = fileMetadata.ProcessingTime,
                Author = fileMetadata.Author,
                CategoryId = fileMetadata.CategoryId,
                CreatedAt = fileMetadata.CreatedAt,
                UpdatedAt = fileMetadata.UpdatedAt
            };

            _logger.LogInformation("Successfully updated file metadata with ID: {Id}", id);
            return Ok(resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while updating file metadata with ID: {Id}", id);
            return StatusCode(500, "An error occurred while updating file metadata");
        }
    }

    /// <summary>
    /// Delete file metadata and all associated S3 objects
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting file metadata and S3 objects for ID: {Id}", id);

            var fileMetadata = await _repository.GetByIdAsync(id);
            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata with ID {Id} not found", id);
                return NotFound($"File metadata with ID {id} not found");
            }

            // Delete all S3 objects associated with this file metadata
            var deleteErrors = new List<string>();

            // Delete from markdown-ocr bucket (required field)
            if (!string.IsNullOrEmpty(fileMetadata.ObjectKeyMarkdownOcr))
            {
                _logger.LogInformation("Deleting {ObjectKey} from {Bucket}",
                    fileMetadata.ObjectKeyMarkdownOcr, S3Buckets.MarkdownOcr);

                var deleted = await _s3Service.DeleteFileAsync(
                    fileMetadata.ObjectKeyMarkdownOcr,
                    S3Buckets.MarkdownOcr
                );

                if (!deleted)
                {
                    deleteErrors.Add($"Failed to delete {fileMetadata.ObjectKeyMarkdownOcr} from {S3Buckets.MarkdownOcr}");
                }
            }

            // Delete from document-summary bucket (optional)
            if (!string.IsNullOrEmpty(fileMetadata.ObjectKeyDocumentSummary))
            {
                _logger.LogInformation("Deleting {ObjectKey} from {Bucket}",
                    fileMetadata.ObjectKeyDocumentSummary, S3Buckets.DocumentSummary);

                var deleted = await _s3Service.DeleteFileAsync(
                    fileMetadata.ObjectKeyDocumentSummary,
                    S3Buckets.DocumentSummary
                );

                if (!deleted)
                {
                    deleteErrors.Add($"Failed to delete {fileMetadata.ObjectKeyDocumentSummary} from {S3Buckets.DocumentSummary}");
                }
            }

            // Delete from chunk-qa bucket (optional)
            if (!string.IsNullOrEmpty(fileMetadata.ObjectKeyChunkQa))
            {
                _logger.LogInformation("Deleting {ObjectKey} from {Bucket}",
                    fileMetadata.ObjectKeyChunkQa, S3Buckets.ChunkQa);

                var deleted = await _s3Service.DeleteFileAsync(
                    fileMetadata.ObjectKeyChunkQa,
                    S3Buckets.ChunkQa
                );

                if (!deleted)
                {
                    deleteErrors.Add($"Failed to delete {fileMetadata.ObjectKeyChunkQa} from {S3Buckets.ChunkQa}");
                }
            }

            // Log any S3 deletion errors but continue with DB deletion
            if (deleteErrors.Any())
            {
                _logger.LogWarning("Some S3 objects could not be deleted: {Errors}", string.Join(", ", deleteErrors));
            }

            // Delete database record
            _repository.Delete(fileMetadata);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted file metadata with ID: {Id} and associated S3 objects", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while deleting file metadata with ID: {Id}", id);
            return StatusCode(500, "An error occurred while deleting file metadata and associated files");
        }
    }
}
