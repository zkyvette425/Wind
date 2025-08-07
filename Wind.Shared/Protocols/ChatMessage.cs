using System.Text.Json.Serialization;

namespace Wind.Shared.Protocols;

public class ChatMessage : BaseMessage
{
    [JsonPropertyName("sender")]
    public string Sender { get; set; } = string.Empty;

    [JsonPropertyName("recipient")]
    public string Recipient { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "global";
}