using System.Text.Json.Serialization;

namespace Wind.Shared.Protocols;

public class PositionUpdateMessage : BaseMessage
{
    [JsonPropertyName("player_id")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }
}