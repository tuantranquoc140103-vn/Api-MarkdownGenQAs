using MarkdownGenQAs.Models.DB;
using Microsoft.EntityFrameworkCore;

[Index(nameof(Name), IsUnique = true)]
public class CategoryFile : BaseEntity
{
    public required string Name { get; set; }
    public List<FileMetadata>? FileMetadatas { get; set; }
}