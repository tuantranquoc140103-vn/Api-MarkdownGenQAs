using System.Text.Json.Serialization;


public class ChunkInfo
{
    [JsonPropertyName("type")]
    public TypeChunk Type { get; set; }
    [JsonPropertyName("tokens_count")]
    public int TokensCount { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; } = string.Empty;
    [JsonPropertyName("tittle_hirarchy")]
    public string? TittleHirarchy { get; set; } = string.Empty;
    [JsonPropertyName("content")]
    public required string Content { get; set; }
}