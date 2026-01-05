
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

public class JsonService : IJsonService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly JsonSchemaExporterOptions _jsonSchemaExporterOptions;

    public JsonService()
    {
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Giữ nguyên < > "
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new TableQACategoryConverter(),
            }
        };

        _jsonSchemaExporterOptions = new()
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

    public string Serialize(object obj) => JsonSerializer.Serialize(obj, _jsonSerializerOptions);

    public T Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json)) return default!;
        return JsonSerializer.Deserialize<T>(json, _jsonSerializerOptions)!;
    }

    public JsonObject CreatejsonChema<T>() where T : class
    {
        var result = JsonSchemaExporter.GetJsonSchemaAsNode(
            _jsonSerializerOptions,
            typeof(T),
            _jsonSchemaExporterOptions
        );

        return result.AsObject();
    }

    public JsonNode SerializeToNode(object obj)
    {
        if(obj is null) throw new ArgumentNullException(nameof(obj));    
        return JsonSerializer.SerializeToNode(obj, _jsonSerializerOptions)!;
    }
}

public class TableQACategoryConverter : JsonConverter<TableQACategory>
{
    public override TableQACategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (Enum.TryParse<TableQACategory>(value, ignoreCase: true, out var result))
            return result;

        // fallback
        return TableQACategory.Other;
    }

    public override void Write(Utf8JsonWriter writer, TableQACategory value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}