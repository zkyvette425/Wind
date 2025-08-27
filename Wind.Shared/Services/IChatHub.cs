using MagicOnion;
using System;
using System.Threading.Tasks;

namespace Wind.Shared.Services
{
    /// <summary>
    /// 聊天StreamingHub接口 - 专门处理实时聊天和消息推送
    /// 支持房间聊天、私聊、系统通知、消息历史等功能
    /// </summary>
    public interface IChatHub : IStreamingHub<IChatHub, IChatHubReceiver>
    {
        #region 连接管理

        /// <summary>
        /// 连接聊天服务 - 验证身份并建立连接
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="accessToken">JWT访问令牌</param>
        /// <returns></returns>
        ValueTask ConnectAsync(string playerId, string accessToken);

        /// <summary>
        /// 断开聊天服务连接
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns></returns>
        ValueTask DisconnectAsync(string playerId);

        #endregion

        #region 房间聊天

        /// <summary>
        /// 加入房间聊天频道
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <returns></returns>
        ValueTask JoinRoomChatAsync(string roomId, string playerId);

        /// <summary>
        /// 离开房间聊天频道
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <returns></returns>
        ValueTask LeaveRoomChatAsync(string roomId, string playerId);

        /// <summary>
        /// 发送房间聊天消息
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">发送者ID</param>
        /// <param name="message">消息内容</param>
        /// <param name="messageType">消息类型 (Text/Emoji/Image等)</param>
        /// <returns></returns>
        ValueTask SendRoomChatAsync(string roomId, string playerId, string message, string messageType = "Text");

        /// <summary>
        /// 获取房间聊天历史
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">请求者ID</param>
        /// <param name="pageIndex">页码</param>
        /// <param name="pageSize">每页大小</param>
        /// <returns></returns>
        ValueTask GetRoomChatHistoryAsync(string roomId, string playerId, int pageIndex = 0, int pageSize = 50);

        #endregion

        #region 私人聊天

        /// <summary>
        /// 发送私聊消息
        /// </summary>
        /// <param name="fromPlayerId">发送者ID</param>
        /// <param name="toPlayerId">接收者ID</param>
        /// <param name="message">消息内容</param>
        /// <param name="messageType">消息类型</param>
        /// <returns></returns>
        ValueTask SendPrivateMessageAsync(string fromPlayerId, string toPlayerId, string message, string messageType = "Text");

        /// <summary>
        /// 获取私聊历史
        /// </summary>
        /// <param name="playerId1">玩家1 ID</param>
        /// <param name="playerId2">玩家2 ID</param>
        /// <param name="requesterId">请求者ID</param>
        /// <param name="pageIndex">页码</param>
        /// <param name="pageSize">每页大小</param>
        /// <returns></returns>
        ValueTask GetPrivateChatHistoryAsync(string playerId1, string playerId2, string requesterId, int pageIndex = 0, int pageSize = 50);

        /// <summary>
        /// 标记私聊消息为已读
        /// </summary>
        /// <param name="playerId">当前玩家ID</param>
        /// <param name="fromPlayerId">消息发送者ID</param>
        /// <param name="messageId">消息ID</param>
        /// <returns></returns>
        ValueTask MarkPrivateMessageAsReadAsync(string playerId, string fromPlayerId, string messageId);

        #endregion

        #region 全局聊天

        /// <summary>
        /// 加入全局聊天频道
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="channelName">频道名称 (World/Trade/Help等)</param>
        /// <returns></returns>
        ValueTask JoinGlobalChannelAsync(string playerId, string channelName);

        /// <summary>
        /// 离开全局聊天频道
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="channelName">频道名称</param>
        /// <returns></returns>
        ValueTask LeaveGlobalChannelAsync(string playerId, string channelName);

        /// <summary>
        /// 发送全局频道消息
        /// </summary>
        /// <param name="playerId">发送者ID</param>
        /// <param name="channelName">频道名称</param>
        /// <param name="message">消息内容</param>
        /// <param name="messageType">消息类型</param>
        /// <returns></returns>
        ValueTask SendGlobalChannelMessageAsync(string playerId, string channelName, string message, string messageType = "Text");

        #endregion

        #region 消息管理

        /// <summary>
        /// 删除消息 (发送者或管理员权限)
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="requesterId">请求者ID</param>
        /// <returns></returns>
        ValueTask DeleteMessageAsync(string messageId, string requesterId);

        /// <summary>
        /// 举报消息
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="reporterId">举报者ID</param>
        /// <param name="reason">举报原因</param>
        /// <returns></returns>
        ValueTask ReportMessageAsync(string messageId, string reporterId, string reason);

        /// <summary>
        /// 屏蔽玩家消息
        /// </summary>
        /// <param name="playerId">当前玩家ID</param>
        /// <param name="blockedPlayerId">被屏蔽玩家ID</param>
        /// <returns></returns>
        ValueTask BlockPlayerAsync(string playerId, string blockedPlayerId);

        /// <summary>
        /// 取消屏蔽玩家
        /// </summary>
        /// <param name="playerId">当前玩家ID</param>
        /// <param name="unblockedPlayerId">解除屏蔽玩家ID</param>
        /// <returns></returns>
        ValueTask UnblockPlayerAsync(string playerId, string unblockedPlayerId);

        #endregion

        #region 系统消息

        /// <summary>
        /// 发送系统通知 (管理员权限)
        /// </summary>
        /// <param name="targetType">目标类型 (All/Room/Player)</param>
        /// <param name="targetId">目标ID (房间ID或玩家ID)</param>
        /// <param name="notificationType">通知类型</param>
        /// <param name="title">通知标题</param>
        /// <param name="content">通知内容</param>
        /// <returns></returns>
        ValueTask SendSystemNotificationAsync(string targetType, string targetId, string notificationType, string title, string content);

        /// <summary>
        /// 获取未读通知数量
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns></returns>
        ValueTask<int> GetUnreadNotificationCountAsync(string playerId);

        /// <summary>
        /// 标记通知为已读
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="notificationId">通知ID</param>
        /// <returns></returns>
        ValueTask MarkNotificationAsReadAsync(string playerId, string notificationId);

        #endregion

        #region 表情和特殊功能

        /// <summary>
        /// 发送表情反应
        /// </summary>
        /// <param name="messageId">目标消息ID</param>
        /// <param name="playerId">反应者ID</param>
        /// <param name="emojiCode">表情代码</param>
        /// <returns></returns>
        ValueTask SendEmojiReactionAsync(string messageId, string playerId, string emojiCode);

        /// <summary>
        /// 发送快捷语音消息
        /// </summary>
        /// <param name="roomId">房间ID (可选，如果是房间语音)</param>
        /// <param name="fromPlayerId">发送者ID</param>
        /// <param name="toPlayerId">接收者ID (可选，如果是私聊语音)</param>
        /// <param name="voiceMessageId">语音消息ID</param>
        /// <param name="duration">语音时长(秒)</param>
        /// <returns></returns>
        ValueTask SendVoiceMessageAsync(string? roomId, string fromPlayerId, string? toPlayerId, string voiceMessageId, int duration);

        #endregion
    }
}

namespace Wind.Shared.Services
{
    /// <summary>
    /// 聊天Hub接收器接口 - 服务器向客户端推送聊天相关消息
    /// </summary>
    public interface IChatHubReceiver
    {
        #region 连接事件

        /// <summary>
        /// 聊天服务连接成功
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="serverTime">服务器时间</param>
        void OnChatConnected(string playerId, long serverTime);

        /// <summary>
        /// 聊天服务连接断开
        /// </summary>
        /// <param name="reason">断开原因</param>
        void OnChatDisconnected(string reason);

        #endregion

        #region 房间聊天事件

        /// <summary>
        /// 接收房间聊天消息
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="messageId">消息ID</param>
        /// <param name="senderId">发送者ID</param>
        /// <param name="senderName">发送者昵称</param>
        /// <param name="message">消息内容</param>
        /// <param name="messageType">消息类型</param>
        /// <param name="timestamp">发送时间</param>
        void OnRoomChatMessage(string roomId, string messageId, string senderId, string senderName, string message, string messageType, long timestamp);

        /// <summary>
        /// 接收房间聊天历史
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="messages">消息列表 (JSON格式)</param>
        /// <param name="totalCount">总消息数</param>
        /// <param name="pageIndex">当前页码</param>
        void OnRoomChatHistory(string roomId, string messages, int totalCount, int pageIndex);

        /// <summary>
        /// 房间聊天频道状态更新
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="onlineCount">在线人数</param>
        /// <param name="isJoined">是否已加入</param>
        void OnRoomChatStatusUpdate(string roomId, int onlineCount, bool isJoined);

        #endregion

        #region 私聊事件

        /// <summary>
        /// 接收私聊消息
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="fromPlayerId">发送者ID</param>
        /// <param name="fromPlayerName">发送者昵称</param>
        /// <param name="message">消息内容</param>
        /// <param name="messageType">消息类型</param>
        /// <param name="timestamp">发送时间</param>
        void OnPrivateMessage(string messageId, string fromPlayerId, string fromPlayerName, string message, string messageType, long timestamp);

        /// <summary>
        /// 接收私聊历史
        /// </summary>
        /// <param name="chatPartnerId">聊天对象ID</param>
        /// <param name="messages">消息列表 (JSON格式)</param>
        /// <param name="totalCount">总消息数</param>
        /// <param name="pageIndex">当前页码</param>
        void OnPrivateChatHistory(string chatPartnerId, string messages, int totalCount, int pageIndex);

        /// <summary>
        /// 私聊消息状态更新
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="status">消息状态 (Sent/Delivered/Read)</param>
        void OnPrivateMessageStatusUpdate(string messageId, string status);

        #endregion

        #region 全局频道事件

        /// <summary>
        /// 接收全局频道消息
        /// </summary>
        /// <param name="channelName">频道名称</param>
        /// <param name="messageId">消息ID</param>
        /// <param name="senderId">发送者ID</param>
        /// <param name="senderName">发送者昵称</param>
        /// <param name="message">消息内容</param>
        /// <param name="messageType">消息类型</param>
        /// <param name="timestamp">发送时间</param>
        void OnGlobalChannelMessage(string channelName, string messageId, string senderId, string senderName, string message, string messageType, long timestamp);

        /// <summary>
        /// 全局频道状态更新
        /// </summary>
        /// <param name="channelName">频道名称</param>
        /// <param name="onlineCount">在线人数</param>
        /// <param name="isJoined">是否已加入</param>
        void OnGlobalChannelStatusUpdate(string channelName, int onlineCount, bool isJoined);

        #endregion

        #region 系统通知事件

        /// <summary>
        /// 接收系统通知
        /// </summary>
        /// <param name="notificationId">通知ID</param>
        /// <param name="notificationType">通知类型</param>
        /// <param name="title">通知标题</param>
        /// <param name="content">通知内容</param>
        /// <param name="priority">优先级</param>
        /// <param name="timestamp">通知时间</param>
        void OnSystemNotification(string notificationId, string notificationType, string title, string content, int priority, long timestamp);

        /// <summary>
        /// 未读通知数量更新
        /// </summary>
        /// <param name="totalUnread">总未读数</param>
        /// <param name="unreadByType">按类型分组的未读数 (JSON格式)</param>
        void OnUnreadNotificationCountUpdate(int totalUnread, string unreadByType);

        #endregion

        #region 消息管理事件

        /// <summary>
        /// 消息被删除通知
        /// </summary>
        /// <param name="messageId">被删除的消息ID</param>
        /// <param name="deletedBy">删除操作者ID</param>
        /// <param name="reason">删除原因</param>
        void OnMessageDeleted(string messageId, string deletedBy, string reason);

        /// <summary>
        /// 表情反应更新
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="reactions">反应列表 (JSON格式)</param>
        void OnEmojiReactionUpdate(string messageId, string reactions);

        /// <summary>
        /// 玩家屏蔽状态更新
        /// </summary>
        /// <param name="blockedPlayerId">被屏蔽玩家ID</param>
        /// <param name="isBlocked">是否被屏蔽</param>
        void OnPlayerBlockStatusUpdate(string blockedPlayerId, bool isBlocked);

        #endregion

        #region 语音消息事件

        /// <summary>
        /// 接收语音消息
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="fromPlayerId">发送者ID</param>
        /// <param name="fromPlayerName">发送者昵称</param>
        /// <param name="voiceMessageId">语音消息ID</param>
        /// <param name="duration">语音时长</param>
        /// <param name="timestamp">发送时间</param>
        void OnVoiceMessage(string messageId, string fromPlayerId, string fromPlayerName, string voiceMessageId, int duration, long timestamp);

        #endregion

        #region 错误和警告事件

        /// <summary>
        /// 聊天错误通知
        /// </summary>
        /// <param name="errorCode">错误代码</param>
        /// <param name="errorMessage">错误信息</param>
        void OnChatError(string errorCode, string errorMessage);

        /// <summary>
        /// 聊天警告通知
        /// </summary>
        /// <param name="warningType">警告类型</param>
        /// <param name="warningMessage">警告信息</param>
        void OnChatWarning(string warningType, string warningMessage);

        #endregion
    }
}