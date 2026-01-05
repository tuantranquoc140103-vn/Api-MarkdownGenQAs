
using System.Text.Json.Nodes;

public interface IJsonService
{
    public string Serialize(object obj);
    public T Deserialize<T>(string json);
    JsonObject CreatejsonChema<T>() where T : class;
    JsonNode SerializeToNode(object obj);
}