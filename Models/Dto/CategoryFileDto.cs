namespace MarkdownGenQAs.Models.Dto;

public class CategoryFileDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<FileMetadataDto>? FileMetadatas { get; set; }
}
