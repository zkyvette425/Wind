using System;

namespace Wind.Shared.Services
{
    /// <summary>
    /// 玩家Hub接收器接口 - 服务器向客户端推送消息的接口
    /// 客户端需要实现此接口来接收服务器的实时推送消息
    /// </summary>
    public interface IPlayerHubReceiver
    {
        #region 连接状态事件
        
        /// <summary>
        /// 接收连接成功通知
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="serverTime">服务器时间</param>
        void OnConnected(string playerId, long serverTime);
        
        /// <summary>
        /// 接收断开连接通知
        /// </summary>
        /// <param name="reason">断开原因</param>
        void OnDisconnected(string reason);
        
        /// <summary>
        /// 接收心跳响应
        /// </summary>
        /// <param name="serverTime">服务器时间戳</param>
        void OnHeartbeatResponse(long serverTime);
        
        #endregion
        
        #region 玩家状态事件
        
        /// <summary>
        /// 接收其他玩家上线通知
        /// </summary>
        /// <param name="playerId">上线玩家ID</param>
        /// <param name="playerName">玩家昵称</param>
        void OnPlayerOnline(string playerId, string playerName);
        
        /// <summary>
        /// 接收其他玩家下线通知
        /// </summary>
        /// <param name="playerId">下线玩家ID</param>
        /// <param name="playerName">玩家昵称</param>
        void OnPlayerOffline(string playerId, string playerName);
        
        /// <summary>
        /// 接收玩家状态更新通知
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="playerName">玩家昵称</param>
        /// <param name="newStatus">新状态</param>
        void OnPlayerStatusChanged(string playerId, string playerName, string newStatus);
        
        /// <summary>
        /// 接收玩家位置更新通知
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="z">Z坐标</param>
        /// <param name="timestamp">更新时间戳</param>
        void OnPlayerPositionUpdated(string playerId, float x, float y, float z, long timestamp);
        
        #endregion
        
        #region 房间事件
        
        /// <summary>
        /// 接收房间加入成功通知
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="roomName">房间名称</param>
        /// <param name="playerCount">当前玩家数</param>
        void OnRoomJoined(string roomId, string roomName, int playerCount);
        
        /// <summary>
        /// 接收房间离开通知
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="reason">离开原因</param>
        void OnRoomLeft(string roomId, string reason);
        
        /// <summary>
        /// 接收其他玩家加入房间通知
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">加入玩家ID</param>
        /// <param name="playerName">玩家昵称</param>
        void OnPlayerJoinedRoom(string roomId, string playerId, string playerName);
        
        /// <summary>
        /// 接收其他玩家离开房间通知
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">离开玩家ID</param>
        /// <param name="playerName">玩家昵称</param>
        void OnPlayerLeftRoom(string roomId, string playerId, string playerName);
        
        #endregion
        
        #region 消息事件
        
        /// <summary>
        /// 接收房间聊天消息
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="senderId">发送者ID</param>
        /// <param name="senderName">发送者昵称</param>
        /// <param name="message">消息内容</param>
        /// <param name="timestamp">发送时间</param>
        void OnRoomMessage(string roomId, string senderId, string senderName, string message, long timestamp);
        
        /// <summary>
        /// 接收私聊消息
        /// </summary>
        /// <param name="fromPlayerId">发送者ID</param>
        /// <param name="fromPlayerName">发送者昵称</param>
        /// <param name="message">消息内容</param>
        /// <param name="timestamp">发送时间</param>
        void OnPrivateMessage(string fromPlayerId, string fromPlayerName, string message, long timestamp);
        
        /// <summary>
        /// 接收系统通知
        /// </summary>
        /// <param name="notificationType">通知类型</param>
        /// <param name="title">通知标题</param>
        /// <param name="content">通知内容</param>
        /// <param name="timestamp">通知时间</param>
        void OnSystemNotification(string notificationType, string title, string content, long timestamp);
        
        #endregion
        
        #region 匹配事件
        
        /// <summary>
        /// 接收匹配队列加入成功通知
        /// </summary>
        /// <param name="queueId">队列ID</param>
        /// <param name="estimatedWaitTime">预计等待时间(秒)</param>
        void OnMatchmakingJoined(string queueId, int estimatedWaitTime);
        
        /// <summary>
        /// 接收匹配成功通知
        /// </summary>
        /// <param name="roomId">匹配到的房间ID</param>
        /// <param name="playerList">房间内玩家列表 (JSON格式)</param>
        void OnMatchFound(string roomId, string playerList);
        
        /// <summary>
        /// 接收匹配取消通知
        /// </summary>
        /// <param name="reason">取消原因</param>
        void OnMatchmakingCancelled(string reason);
        
        #endregion
        
        #region 游戏事件
        
        /// <summary>
        /// 接收玩家准备状态更新通知
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="playerName">玩家昵称</param>
        /// <param name="isReady">是否准备</param>
        void OnPlayerReadyStatusChanged(string playerId, string playerName, bool isReady);
        
        /// <summary>
        /// 接收游戏开始通知
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="gameSettings">游戏设置 (JSON格式)</param>
        /// <param name="startTimestamp">游戏开始时间</param>
        void OnGameStart(string roomId, string gameSettings, long startTimestamp);
        
        /// <summary>
        /// 接收游戏结束通知
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="gameResult">游戏结果 (JSON格式)</param>
        /// <param name="endTimestamp">游戏结束时间</param>
        void OnGameEnd(string roomId, string gameResult, long endTimestamp);
        
        /// <summary>
        /// 接收游戏状态更新通知
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="gameState">游戏状态数据 (JSON格式)</param>
        /// <param name="timestamp">更新时间</param>
        void OnGameStateUpdate(string roomId, string gameState, long timestamp);
        
        #endregion
        
        #region 错误和异常事件
        
        /// <summary>
        /// 接收错误通知
        /// </summary>
        /// <param name="errorCode">错误代码</param>
        /// <param name="errorMessage">错误信息</param>
        void OnError(string errorCode, string errorMessage);
        
        /// <summary>
        /// 接收警告通知
        /// </summary>
        /// <param name="warningType">警告类型</param>
        /// <param name="warningMessage">警告信息</param>
        void OnWarning(string warningType, string warningMessage);
        
        #endregion
    }
}