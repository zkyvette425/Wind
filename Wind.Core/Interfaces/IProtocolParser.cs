using Wind.Shared.Protocols;

namespace Wind.Core.Interfaces;

public interface IProtocolParser
{
    /// <summary>
    /// 解析JSON字符串为消息对象
    /// </summary>
    /// <param name="json">JSON字符串</param>
    /// <returns>解析后的消息对象</returns>
    BaseMessage? ParseMessage(string json);

    /// <summary>
    /// 将消息对象序列化为JSON字符串
    /// </summary>
    /// <param name="message">消息对象</param>
    /// <returns>序列化后的JSON字符串</returns>
    string SerializeMessage(BaseMessage message);
}