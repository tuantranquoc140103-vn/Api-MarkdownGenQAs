using System.Runtime.CompilerServices;

public interface IGenQAsService
{
    Task<List<SummaryQA>> GenQAsSumaryAsync(string dataSource, string nameFile);
    Task<string> GenSummaryDocumentAsync(string dataSource, string nameFile);
    Task<List<TextQA>> GenQAsTextAsync(ChunkInfo chunkInfo, string summaryDocument, string nameFile);
    Task<List<TableQA>> GenQAsTableAsync(ChunkInfo chunkInfo, string summaryDocument, string nameFile);
}