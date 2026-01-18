using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MarkdownGenQAs.Models.QA;

public class QA
{
    [Description("Câu hỏi cho tài liệu")]
    [Required]
    [JsonPropertyName("question")]
    public required string Question { get; set; }

    [Description("Câu trả lời chi tiết nhưng súc tích, trích xuất từ tài liệu")]
    [Required]
    [JsonPropertyName("answer")]
    public required string Answer { get; set; }
}