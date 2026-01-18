using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MarkdownGenQAs.Models.QA;
public class SummaryQA : QA
{
    [Description("Phân loại mục đích của câu hỏi: Objective, Audience, KeyTopics, Takeaways, hoặc Scope")]
    [JsonPropertyName("category")]
    public SummaryCategory Category { get; set; }
}

public enum SummaryCategory
{
    [Description("Mục tiêu chính của tài liệu")]
    Objective,
    
    [Description("Đối tượng độc giả mục tiêu")]
    Audience,
    
    [Description("Các chủ đề hoặc chương mục chính")]
    KeyTopics,
    
    [Description("Những điểm rút ra quan trọng nhất")]
    Takeaways,
    
    [Description("Phạm vi và giới hạn của tài liệu")]
    Scope
}