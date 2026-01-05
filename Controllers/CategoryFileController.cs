using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MarkdownGenQAs.Models.Dto;

namespace MarkdownGenQAs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class CategoryFileController : ControllerBase
{
    private readonly ICategoryFileRepository _repository;
    private readonly ILogger<CategoryFileController> _logger;

    public CategoryFileController(
        ICategoryFileRepository repository,
        ILogger<CategoryFileController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all category files
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryFileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryFileDto>>> GetAll()
    {
        try
        {
            _logger.LogInformation("Getting all category files");
            var categoryFiles = await _repository.GetAllAsync();
            
            var dtos = categoryFiles.Select(cf => new CategoryFileDto
            {
                Id = cf.Id,
                Name = cf.Name,
                CreatedAt = cf.CreatedAt,
                UpdatedAt = cf.UpdatedAt,
                FileMetadatas = cf.FileMetadatas?.Select(fm => new FileMetadataDto
                {
                    Id = fm.Id,
                    FileName = fm.FileName,
                    FileType = fm.FileType.ToString(),
                    Status = fm.Status.ToString()
                }).ToList()
            });

            _logger.LogInformation("Successfully retrieved {Count} category files", categoryFiles.Count());
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting all category files");
            return StatusCode(500, "An error occurred while retrieving category files");
        }
    }

    /// <summary>
    /// Get category file by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CategoryFileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryFileDto>> GetById(Guid id)
    {
        try
        {
            _logger.LogInformation("Getting category file with ID: {Id}", id);
            var categoryFile = await _repository.GetByIdAsync(id);

            if (categoryFile == null)
            {
                _logger.LogWarning("Category file with ID {Id} not found", id);
                return NotFound($"Category file with ID {id} not found");
            }

            var dto = new CategoryFileDto
            {
                Id = categoryFile.Id,
                Name = categoryFile.Name,
                CreatedAt = categoryFile.CreatedAt,
                UpdatedAt = categoryFile.UpdatedAt,
                FileMetadatas = categoryFile.FileMetadatas?.Select(fm => new FileMetadataDto
                {
                    Id = fm.Id,
                    FileName = fm.FileName,
                    FileType = fm.FileType.ToString(),
                    Status = fm.Status.ToString()
                }).ToList()
            };

            _logger.LogInformation("Successfully retrieved category file with ID: {Id}", id);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting category file with ID: {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the category file");
        }
    }

    /// <summary>
    /// Create a new category file
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryFileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryFileDto>> Create([FromBody] CreateCategoryFileDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for creating category file");
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Creating new category file with name: {Name}", dto.Name);

            var categoryFile = new CategoryFile
            {
                Name = dto.Name
            };

            await _repository.AddAsync(categoryFile);
            await _repository.SaveChangesAsync();

            var resultDto = new CategoryFileDto
            {
                Id = categoryFile.Id,
                Name = categoryFile.Name,
                CreatedAt = categoryFile.CreatedAt,
                UpdatedAt = categoryFile.UpdatedAt
            };

            _logger.LogInformation("Successfully created category file with ID: {Id}", categoryFile.Id);
            return CreatedAtAction(nameof(GetById), new { id = categoryFile.Id }, resultDto);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true || 
                                           ex.InnerException?.Message.Contains("unique constraint") == true)
        {
            _logger.LogWarning("Duplicate category file name: {Name}", dto.Name);
            return BadRequest($"A category file with the name '{dto.Name}' already exists");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating category file");
            return StatusCode(500, "An error occurred while creating the category file");
        }
    }

    /// <summary>
    /// Update an existing category file
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CategoryFileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryFileDto>> Update(Guid id, [FromBody] UpdateCategoryFileDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for updating category file with ID: {Id}", id);
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Updating category file with ID: {Id}", id);

            var categoryFile = await _repository.GetByIdAsync(id);
            if (categoryFile == null)
            {
                _logger.LogWarning("Category file with ID {Id} not found", id);
                return NotFound($"Category file with ID {id} not found");
            }

            categoryFile.Name = dto.Name;
            _repository.Update(categoryFile);
            await _repository.SaveChangesAsync();

            var resultDto = new CategoryFileDto
            {
                Id = categoryFile.Id,
                Name = categoryFile.Name,
                CreatedAt = categoryFile.CreatedAt,
                UpdatedAt = categoryFile.UpdatedAt
            };

            _logger.LogInformation("Successfully updated category file with ID: {Id}", id);
            return Ok(resultDto);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true || 
                                           ex.InnerException?.Message.Contains("unique constraint") == true)
        {
            _logger.LogWarning("Duplicate category file name: {Name}", dto.Name);
            return BadRequest($"A category file with the name '{dto.Name}' already exists");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while updating category file with ID: {Id}", id);
            return StatusCode(500, "An error occurred while updating the category file");
        }
    }

    /// <summary>
    /// Delete a category file
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting category file with ID: {Id}", id);

            var categoryFile = await _repository.GetByIdAsync(id);
            if (categoryFile == null)
            {
                _logger.LogWarning("Category file with ID {Id} not found", id);
                return NotFound($"Category file with ID {id} not found");
            }

            _repository.Delete(categoryFile);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted category file with ID: {Id}", id);
            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error occurred while deleting category file with ID: {Id}", id);
            return BadRequest("Cannot delete this category file because it has associated file metadata. Please delete the associated files first.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while deleting category file with ID: {Id}", id);
            return StatusCode(500, "An error occurred while deleting the category file");
        }
    }
}
