using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MarkdownGenQAs.Models.QA;

public class TextQAInfor
{
    [JsonPropertyName("chunk_infor")]
    public required ChunkInfo chunkInfo { get; set; }
    [JsonPropertyName("qas")]
    public required List<TextQA> QAs { get; set; }
}

public class TableQAInfor
{
    [JsonPropertyName("chunk_infor")]
    public required ChunkInfo chunkInfo { get; set; }
    [JsonPropertyName("qas")]
    public required List<TableQA> QAs { get; set; }
}

public class ChunkQA : QA
{
    [Description("Thể loại câu hỏi")]
    [JsonPropertyName("category")]
    public string? Category { get; set; }
}

public class ChunkQAInfor
{
    [JsonPropertyName("chunk_infor")]
    public required ChunkInfo chunkInfo { get; set; }
    [JsonPropertyName("qas")]
    public required List<ChunkQA> QAs { get; set; }
}

