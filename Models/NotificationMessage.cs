
using MarkdownGenQAs.Models.Enum;

namespace MarkdownGenQAs.Models;

public class NotificationMessage
{
    public Guid FileMetadataId { get; set; }
    public string Timestamp { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    public string Message { get; set; } = string.Empty;
    public required string Status { get; set; }
}
