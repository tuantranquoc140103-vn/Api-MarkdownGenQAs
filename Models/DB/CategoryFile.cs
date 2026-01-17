using Microsoft.EntityFrameworkCore;

namespace MarkdownGenQAs.Models.DB;

[Index(nameof(Name), IsUnique = true)]
public class CategoryFile : BaseEntity
{
    public required string Name { get; set; }
    public List<OCRFile>? OCRFiles { get; set; }
}