using System.Text.Json;
using System.Text.Json.Serialization;
using Wind.Core.Interfaces;
using Wind.Shared.Protocols;

namespace Wind.Core.Network;

public class JsonProtocolParser : IProtocolParser
{
    private readonly JsonSerializerOptions _options;

    public JsonProtocolParser()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public BaseMessage? ParseMessage(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BaseMessage>(json, _options);
        }
        catch (Exception ex)
        {
            // 实际应用中应该使用日志记录异常
            Console.WriteLine($"解析消息失败: {ex.Message}");
            return null;
        }
    }

    public string SerializeMessage(BaseMessage message)
    {
        try
        {
            return JsonSerializer.Serialize(message, _options);
        }
        catch (Exception ex)
        {
            // 实际应用中应该使用日志记录异常
            Console.WriteLine($"序列化消息失败: {ex.Message}");
            return string.Empty;
        }
    }
}