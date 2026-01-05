namespace MarkdownGenQAs.Models.Dto;

public class FileMetadataDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    public string ObjectKeyMarkdownOcr { get; set; } = string.Empty;
    public string? ObjectKeyDocumentSummary { get; set; }
    public string? ObjectKeyChunkQa { get; set; }
    
    public int ProcessingTime { get; set; }
    public string? Author { get; set; }
    
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
