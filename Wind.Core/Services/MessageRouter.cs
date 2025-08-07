using Microsoft.Extensions.Logging;
using Wind.Core.Interfaces;
using Wind.Shared.Protocols;

namespace Wind.Core.Services;

public class MessageRouter : IMessageRouter
{
    private readonly ILogger<MessageRouter> _logger;
    private readonly Dictionary<Type, Func<BaseMessage, string, Task>> _handlers;

    public MessageRouter(ILogger<MessageRouter> logger)
    {
        _logger = logger;
        _handlers = new Dictionary<Type, Func<BaseMessage, string, Task>>();

        // 注册消息处理器
        RegisterHandler<LoginMessage>(HandleLoginMessageAsync);
        RegisterHandler<ChatMessage>(HandleChatMessageAsync);
        RegisterHandler<PositionUpdateMessage>(HandlePositionUpdateMessageAsync);
    }

    /// <summary>
    /// 注册消息处理器
    /// </summary>
    /// <typeparam name="TMessage">消息类型</typeparam>
    /// <param name="handler">处理函数</param>
    public void RegisterHandler<TMessage>(Func<TMessage, string, Task> handler) where TMessage : BaseMessage
    {
        _handlers[typeof(TMessage)] = (message, senderId) => handler((TMessage)message, senderId);
    }

    public async Task RouteMessageAsync(BaseMessage message, string senderId)
    {
        if (message == null)
        {
            _logger.LogWarning("接收到空消息");
            return;
        }

        var messageType = message.GetType();
        if (_handlers.TryGetValue(messageType, out var handler))
        {
            try
            {
                await handler(message, senderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理消息时发生错误: {MessageType}", messageType.Name);
            }
        }
        else
        {
            _logger.LogWarning("未找到消息处理器: {MessageType}", messageType.Name);
        }
    }

    private async Task HandleLoginMessageAsync(LoginMessage message, string senderId)
    {
        // 登录逻辑实现
        _logger.LogInformation("处理登录消息: {Username}", message.Username);
        // 实际应用中应该调用认证服务
        await Task.CompletedTask;
    }

    private async Task HandleChatMessageAsync(ChatMessage message, string senderId)
    {
        // 聊天逻辑实现
        _logger.LogInformation("处理聊天消息: {Sender} -> {Recipient}: {Content}",
            message.Sender, message.Recipient, message.Content);
        // 实际应用中应该广播消息给收件人
        await Task.CompletedTask;
    }

    private async Task HandlePositionUpdateMessageAsync(PositionUpdateMessage message, string senderId)
    {
        // 位置更新逻辑实现
        _logger.LogInformation("处理位置更新消息: {PlayerId} at ({X}, {Y}, {Z})",
            message.PlayerId, message.X, message.Y, message.Z);
        // 实际应用中应该更新玩家位置并广播给其他玩家
        await Task.CompletedTask;
    }
}