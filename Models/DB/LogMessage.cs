namespace MarkdownGenQAs.Models.DB;

public class LogMessage : BaseEntity
{
    public string? LogsOCR { get; set; }
    public string? LogsGenQA { get; set; }
    public Guid? OCRFileId { get; set; }
    public OCRFile? OCRFile { get; set; }
}
