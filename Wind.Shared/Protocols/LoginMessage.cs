using System.Text.Json.Serialization;

namespace Wind.Shared.Protocols;

public class LoginMessage : BaseMessage
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}