using System.Text.Json.Serialization;

namespace Wind.Shared.Protocols;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LoginMessage), typeDiscriminator: "login")]
[JsonDerivedType(typeof(ChatMessage), typeDiscriminator: "chat")]
[JsonDerivedType(typeof(PositionUpdateMessage), typeDiscriminator: "position_update")]
public abstract class BaseMessage
{
    [JsonPropertyName("id")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}