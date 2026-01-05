using System.ComponentModel.DataAnnotations;
using MarkdownGenQAs.Models.Enum;

namespace MarkdownGenQAs.Models.Dto;

public class UpdateFileMetadataDto
{
    [StringLength(255)]
    public string? Author { get; set; }
    
    public Guid? CategoryId { get; set; }
    
    public StatusFile? Status { get; set; }
}
