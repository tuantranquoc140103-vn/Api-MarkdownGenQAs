
using MarkdownGenQAs.Models.DB;
using MarkdownGenQAs.Models.Enum;

public class FileMetadata : BaseEntity
{
    public required string FileName { get; set; }
    public required string FileType { get; set; }
    
    // Object Keys for S3 buckets
    public required string ObjectKeyMarkdownOcr { get; set; }
    public string? ObjectKeyDocumentSummary { get; set; }
    public string? ObjectKeyChunkQa { get; set; }
    
    public StatusFile Status { get; set; } = StatusFile.Uploaded;
    public int ProcessingTime { get; set; }
    public string? Author { get; set; }

    public Guid? CategoryId { get; set; }
    public CategoryFile? CategoryFile { get; set; }
    public LogMessage? LogMessage { get; set; }
}