using System.Runtime.CompilerServices;

public interface IGenQAsService
{
    Task<List<ChunkQA>> GenQAsSumaryAsync(string dataSource, string nameFile);
    Task<string> GenSummaryDocumentAsync(string dataSource, string nameFile);
    Task<List<ChunkQA>> GenQAsTextAsync(ChunkInfo chunkInfo, string summaryDocument, string nameFile);
    Task<List<ChunkQA>> GenQAsTableAsync(ChunkInfo chunkInfo, string summaryDocument, string nameFile);
}