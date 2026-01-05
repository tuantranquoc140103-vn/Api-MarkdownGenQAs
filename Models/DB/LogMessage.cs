namespace MarkdownGenQAs.Models.DB;

public class LogMessage : BaseEntity
{
    public required string Message { get; set; }
    public Guid? FileMetadataId { get; set; }
    public FileMetadata? FileMetadata { get; set; }
}
