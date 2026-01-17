using MarkdownGenQAs.Models.Enum;

namespace MarkdownGenQAs.Models.DB;

public class OCRFile : BaseEntity
{
    public required string FileName { get; set; }
    
    // Object Keys for S3 buckets
    public string? ObjectKeyMarkdownOcr { get; set; }
    public required string ObjectKeyFilePdf { get; set; }
    public string? ObjectKeyChunkQa { get; set; }
    
    public StatusFile Status { get; set; } = StatusFile.Uploaded;
    public int ProcessingTime { get; set; }
    public string? Author { get; set; }

    public Guid? CategoryId { get; set; }
    public CategoryFile? CategoryFile { get; set; }
    public LogMessage? LogMessage { get; set; }
    public OCRFileJob? OCRFileJob { get; set; }
}