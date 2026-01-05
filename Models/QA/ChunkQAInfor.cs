using System.Text.Json.Serialization;

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

