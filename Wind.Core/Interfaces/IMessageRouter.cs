using Wind.Shared.Protocols;

namespace Wind.Core.Interfaces;

public interface IMessageRouter
{
    /// <summary>
    /// 路由消息到相应的处理程序
    /// </summary>
    /// <param name="message">要路由的消息</param>
    /// <param name="senderId">发送者ID</param>
    Task RouteMessageAsync(BaseMessage message, string senderId);
}