using System.ComponentModel;
using System.Text.Json.Serialization;
using MarkdownGenQAs.Models.QA;
using Models.Enum;


public class TextQA : QA
{
    [Description("Thể loại câu hỏi")]
    [JsonPropertyName("category")]
    public TextQACategory Category { get; set; }
}