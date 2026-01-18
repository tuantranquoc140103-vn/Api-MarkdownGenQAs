using System.ComponentModel;
using System.Text.Json.Serialization;
using MarkdownGenQAs.Models.Enum;

namespace MarkdownGenQAs.Models.QA;

public class TableQA : QA
{
    [Description("Thể loại câu hỏi")]
    [JsonPropertyName("category")]
    public TableQACategory category { get; set; }
}