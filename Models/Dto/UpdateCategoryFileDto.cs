using System.ComponentModel.DataAnnotations;

namespace MarkdownGenQAs.Models.Dto;

public class UpdateCategoryFileDto
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters")]
    public required string Name { get; set; }
}
