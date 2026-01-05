using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;

public class MarkdownServiceHelper
{
    public static string RemoveTablesFromSource(string source, List<ChunkInfo> tableChunks)
    {
        // Logic để loại bỏ nội dung table khỏi source
        // Implementation phụ thuộc vào cấu trúc dữ liệu của bạn
        foreach (var table in tableChunks)
        {
            source = source.Replace(table.Content, "");
            if (!string.IsNullOrEmpty(table.Title))
            {
                source = source.Replace(table.Title, "");
            }
        }
        return source.Trim();
    }

    public void ShowChunks(List<ChunkInfo> chunks, int maxChar = 100)
    {

        string underline = new string('-', 50);

        foreach (var chunk in chunks)
        {
            Console.WriteLine($"{chunk.Type} - {chunk.TokensCount} tokens");
            Console.WriteLine($"Title Hyrarchy header: {chunk.TittleHirarchy}");
            if (chunk.Content.Length > maxChar)
            {

                Console.WriteLine(chunk.Content[..maxChar]);
                Console.WriteLine("|||||||||");
                Console.WriteLine(chunk.Content[^maxChar..]);
            }
            else
            {
                Console.WriteLine(chunk.Content);

            }

            Console.WriteLine(underline);
        }
    }

    public static List<Block> GetAllBlock(string source, MarkdownPipeline pipeline)
    {
        MarkdownDocument document = Markdown.Parse(source, pipeline);

        return document.ToList<Block>();
    }

    public static List<string> SplitIntoSentences(string text)
    {
        // Regex để tách câu, xử lý các trường hợp: . ! ? kết thúc câu
        var pattern = @"(?<=[.!?])\s+(?=[A-Z])";
        return Regex.Split(text, pattern).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    public static List<string> ExtractListItems(string content)
    {
        // Trích xuất list items (numbered, bullet, dash)
        var pattern = @"(?:^|\n)[\s]*(?:\d+\.|[-*•])\s+(.+?)(?=\n[\s]*(?:\d+\.|[-*•])|\n\n|$)";
        var matches = Regex.Matches(content, pattern, RegexOptions.Singleline);
        return matches.Select(m => m.Value.Trim()).ToList();
    }

    public static string ExtractContentFromBlocks(string source, List<Block> blocks)
    {
        var segments = blocks.Select(b => source.Substring(b.Span.Start, b.Span.Length));
        return string.Join("\n\n", segments);
    }


}