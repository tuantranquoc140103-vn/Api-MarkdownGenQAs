using System.ComponentModel;
using System.Text.Json.Serialization;

public enum TypeChunk
{
    [Description("Table")]
    [JsonStringEnumMemberName("table")]
    Table,
    [Description("All content")]
    [JsonStringEnumMemberName("allContent")]
    Text
}