
public interface IMarkdownService
{
    Task<List<ChunkInfo>> CreateChunkDocument(string source);
    Task<List<ChunkInfo>> CreateChunkTableInSection(string source);
    Task<List<ChunkInfo>> CreateChunkByRegexAsync(string source);
}