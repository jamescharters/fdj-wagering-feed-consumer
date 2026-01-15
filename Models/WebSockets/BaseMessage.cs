using System.Text.Json;
using System.Text.Json.Serialization;

namespace WageringFeedConsumer.Models.WebSockets;

public record BaseMessage
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MessageType Type { get; init; }
    public JsonElement Payload { get; init; }
    public DateTime Timestamp { get; init; }
}