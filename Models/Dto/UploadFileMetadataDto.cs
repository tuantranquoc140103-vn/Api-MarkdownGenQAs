using System.ComponentModel.DataAnnotations;

namespace MarkdownGenQAs.Models.Dto;

public class UploadFileMetadataDto
{
    [Required(ErrorMessage = "File is required")]
    public required IFormFile File { get; set; }
    
    public Guid? CategoryId { get; set; }
    
    [StringLength(255)]
    public string? Author { get; set; }
}
