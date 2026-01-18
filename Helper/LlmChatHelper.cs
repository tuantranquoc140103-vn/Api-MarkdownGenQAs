using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.RegularExpressions;
using GenQAServer.Options;
using MarkdownGenQAs.Models;
using MarkdownGenQAs.Models.Enum;

namespace MarkdownGenQAs.Helper;
public class LlmChatHelper
{

    public static string ParseChoice(string rawResponse, List<string> validChoices)
    {
        if (string.IsNullOrWhiteSpace(rawResponse)) return string.Empty;

        // Bước 1: Nếu model trả về chính xác 1 từ trong list (Case xịn)
        string cleanResponse = rawResponse.Trim();
        var exactMatch = validChoices.FirstOrDefault(c =>
            string.Equals(c, cleanResponse, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null) return exactMatch;

        // Bước 2: Nếu model "nhiều lời", tìm từ khóa xuất hiện cuối cùng trong văn bản
        // Thường đáp án sẽ nằm ở cuối câu: "Thus, the answer is Good."
        string bestMatch = string.Empty;
        int lastIndex = -1;

        foreach (var choice in validChoices)
        {
            // Dùng Regex \b để tránh bắt nhầm (ví dụ "Good" trong "Goodbye")
            var match = Regex.Match(rawResponse, $@"\b{Regex.Escape(choice)}\b", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

            if (match.Success && match.Index > lastIndex)
            {
                lastIndex = match.Index;
                bestMatch = choice;
            }
        }

        return !string.IsNullOrEmpty(bestMatch) ? bestMatch : cleanResponse;
    }

    public static List<ChatMessageRequest> CreateChatMessageChoice(string table1, string table2, Prompt prompt)
    {
        string path = prompt.PathTemplatePrompt;
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found for prompt choice: ", path);
        }
        string templatePromptChoice = File.ReadAllText(path);
        string finalPrompt = string.Format(templatePromptChoice, table1, table2);
        List<ChatMessageRequest> result = new List<ChatMessageRequest>()
        {
            new ChatMessageRequest{Role = ChatRole.System, Content = prompt.SystemPrompt},
            new ChatMessageRequest{Role = ChatRole.User, Content = finalPrompt}
        };
        return result;

    }

    public static string CleanJsonWithWindowsPath(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
            return jsonString;

        // Phương pháp 1: Thay thế tất cả single backslash thành double backslash
        // trừ những backslash đã được escape đúng
        string cleaned = Regex.Replace(jsonString, @"(?<!\\)\\(?![""\\/bfnrtu])", @"\\");

        return cleaned;
    }


    public static string EscapeNewlinesInsideJsonStrings(string input)
    {
        var sb = new StringBuilder();
        bool inString = false; bool escaped = false; foreach (var ch in input) { if (escaped) { sb.Append(ch); escaped = false; continue; } if (ch == '\\') { sb.Append(ch); escaped = true; continue; } if (ch == '"') { sb.Append(ch); inString = !inString; continue; } if (inString && (ch == '\n' || ch == '\r')) { sb.Append("\\n"); continue; } sb.Append(ch); }
        return sb.ToString();
    }

    public static readonly JsonSchemaExporterOptions _llmSchemaExporterOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = (context, node) =>
      {
          // 1. Lấy description từ Property
          var propAttribute = context.PropertyInfo?
              .AttributeProvider?
              .GetCustomAttributes(typeof(DescriptionAttribute), false)
              .FirstOrDefault() as DescriptionAttribute;

          // 2. Nếu không có ở Property, thử lấy ở Type (hữu ích cho Class hoặc Enum)
          var typeAttribute = context.TypeInfo.Type
              .GetCustomAttributes(typeof(DescriptionAttribute), false)
              .FirstOrDefault() as DescriptionAttribute;

          var description = propAttribute?.Description ?? typeAttribute?.Description;

          if (!string.IsNullOrEmpty(description) && node is JsonObject obj)
          {
              // Thêm description vào node JSON
              obj.Insert(0, "description", JsonValue.Create(description));
          }

          return node;
      }
    };
}