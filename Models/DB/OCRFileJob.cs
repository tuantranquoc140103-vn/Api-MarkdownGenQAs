
namespace MarkdownGenQAs.Models.DB;

public class OCRFileJob : BaseEntity
{
    public Guid? OCRFileId { get; set; }
    public OCRFile? OCRFile { get; set; }
    public string? FileJobId { get; set; } // Stores file_id (Record ID)
    public string? WorkerJobId { get; set; } // Stores job_id (Worker ID)
}
