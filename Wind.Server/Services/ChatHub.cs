using MagicOnion.Server.Hubs;
using Microsoft.Extensions.Logging;
using Orleans;
using System.Collections.Concurrent;
using Wind.GrainInterfaces;
using Wind.Shared.Services;
using Wind.Server.Services;

namespace Wind.Server.Services
{
    /// <summary>
    /// 聊天StreamingHub实现
    /// 提供实时聊天功能，包括房间聊天、私聊、系统通知等
    /// 与Orleans Grain集成，支持持久化和分布式架构
    /// </summary>
    public class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
    {
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger<ChatHub> _logger;
        private readonly JwtService _jwtService;

        // 连接管理
        private readonly ConcurrentDictionary<string, string> _playerConnections = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _roomChannels = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _globalChannels = new();
        
        // MagicOnion Group管理 - 保存Group引用以便广播
        private readonly ConcurrentDictionary<string, IGroup<IChatHubReceiver>> _roomGroups = new();
        private readonly ConcurrentDictionary<string, IGroup<IChatHubReceiver>> _globalGroups = new();

        public ChatHub(IGrainFactory grainFactory, ILogger<ChatHub> logger, JwtService jwtService)
        {
            _grainFactory = grainFactory;
            _logger = logger;
            _jwtService = jwtService;
        }

        #region 连接生命周期

        /// <summary>
        /// 客户端连接时触发
        /// </summary>
        protected override ValueTask OnConnected()
        {
            _logger.LogInformation("聊天Hub连接建立中: ConnectionId={ConnectionId}", ConnectionId);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 客户端断开连接时触发
        /// </summary>
        protected override ValueTask OnDisconnected()
        {
            var connectionId = ConnectionId.ToString();
            _logger.LogInformation("聊天Hub连接断开: ConnectionId={ConnectionId}", connectionId);

            // 清理连接信息
            var playersToRemove = _playerConnections
                .Where(pair => pair.Value == connectionId)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var playerId in playersToRemove)
            {
                _playerConnections.TryRemove(playerId, out _);
                
                // 从所有频道中移除玩家
                RemovePlayerFromAllChannels(playerId);
                
                _logger.LogInformation("聊天服务断开连接清理: PlayerId={PlayerId}", playerId);
            }

            return ValueTask.CompletedTask;
        }

        #endregion

        #region 连接管理API实现

        /// <summary>
        /// 连接聊天服务 - 验证身份并建立连接
        /// </summary>
        public async ValueTask ConnectAsync(string playerId, string accessToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(accessToken))
                {
                    Client.OnChatError("INVALID_PARAMS", "玩家ID和访问令牌不能为空");
                    return;
                }

                // 验证JWT令牌
                var validationResult = _jwtService.ValidateAccessToken(accessToken);
                if (!validationResult.IsValid)
                {
                    Client.OnChatError("INVALID_TOKEN", "访问令牌无效或已过期");
                    return;
                }

                // 验证令牌中的玩家ID
                var tokenPlayerId = _jwtService.ExtractPlayerIdFromToken(accessToken);
                if (tokenPlayerId != playerId)
                {
                    Client.OnChatError("TOKEN_PLAYER_MISMATCH", "令牌中的玩家ID与请求的不匹配");
                    return;
                }

                _logger.LogInformation("聊天服务连接成功: PlayerId={PlayerId}, ConnectionId={ConnectionId}",
                    playerId, ConnectionId);

                // 记录连接
                _playerConnections.AddOrUpdate(playerId, ConnectionId.ToString(), (key, oldValue) => ConnectionId.ToString());

                // 更新玩家在线状态
                try
                {
                    var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                    await playerGrain.HeartbeatAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "更新玩家在线状态失败: PlayerId={PlayerId}", playerId);
                }

                // 通知客户端连接成功
                Client.OnChatConnected(playerId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "聊天服务连接失败: PlayerId={PlayerId}", playerId);
                Client.OnChatError("CONNECTION_FAILED", "聊天服务连接失败");
            }
        }

        /// <summary>
        /// 断开聊天服务连接
        /// </summary>
        public async ValueTask DisconnectAsync(string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return;
                }

                _logger.LogInformation("聊天服务主动断开: PlayerId={PlayerId}", playerId);

                // 从连接记录中移除
                if (_playerConnections.TryRemove(playerId, out var connectionId))
                {
                    // 从所有频道中移除
                    RemovePlayerFromAllChannels(playerId);
                    
                    // 通知客户端断开
                    Client.OnChatDisconnected("主动断开");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "断开聊天服务失败: PlayerId={PlayerId}", playerId);
            }
        }

        #endregion

        #region 房间聊天API实现

        /// <summary>
        /// 加入房间聊天频道
        /// </summary>
        public async ValueTask JoinRoomChatAsync(string roomId, string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                {
                    Client.OnChatError("INVALID_PARAMS", "房间ID和玩家ID不能为空");
                    return;
                }

                // 验证玩家是否在房间中
                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var currentRoomId = await playerGrain.GetCurrentRoomAsync();
                
                if (currentRoomId != roomId)
                {
                    Client.OnChatError("NOT_IN_ROOM", "玩家不在指定房间中");
                    return;
                }

                _logger.LogInformation("玩家加入房间聊天: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);

                // 加入房间群组并保存引用
                var roomKey = $"room_{roomId}";
                var roomGroup = await Group.AddAsync(roomKey);
                _roomGroups.AddOrUpdate(roomKey, roomGroup, (key, oldGroup) => roomGroup);
                
                // 记录到房间频道
                _roomChannels.AddOrUpdate(roomKey, new HashSet<string> { playerId }, 
                    (key, existingSet) => 
                    {
                        lock (existingSet)
                        {
                            existingSet.Add(playerId);
                            return existingSet;
                        }
                    });

                // 获取房间在线人数
                var onlineCount = _roomChannels.ContainsKey(roomKey) ? _roomChannels[roomKey].Count : 0;

                // 通知房间内所有玩家
                roomGroup.All.OnRoomChatStatusUpdate(roomId, onlineCount, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加入房间聊天失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
                Client.OnChatError("JOIN_ROOM_CHAT_FAILED", "加入房间聊天失败");
            }
        }

        /// <summary>
        /// 离开房间聊天频道
        /// </summary>
        public async ValueTask LeaveRoomChatAsync(string roomId, string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                {
                    return;
                }

                _logger.LogInformation("玩家离开房间聊天: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);

                var roomKey = $"room_{roomId}";
                
                // 从房间群组中正确移除
                if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
                {
                    await roomGroup.RemoveAsync(Context);
                }
                
                // 从房间频道记录中移除
                if (_roomChannels.TryGetValue(roomKey, out var playerSet))
                {
                    lock (playerSet)
                    {
                        playerSet.Remove(playerId);
                        if (playerSet.Count == 0)
                        {
                            _roomChannels.TryRemove(roomKey, out _);
                        }
                    }
                }

                // 获取房间在线人数
                var onlineCount = _roomChannels.ContainsKey(roomKey) ? _roomChannels[roomKey].Count : 0;

                // 通知房间内所有玩家聊天状态更新
                if (_roomGroups.TryGetValue(roomKey, out var broadcastGroup))
                {
                    broadcastGroup.All.OnRoomChatStatusUpdate(roomId, onlineCount, false);
                }
                else
                {
                    Client.OnRoomChatStatusUpdate(roomId, onlineCount, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "离开房间聊天失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
        }

        /// <summary>
        /// 发送房间聊天消息
        /// </summary>
        public async ValueTask SendRoomChatAsync(string roomId, string playerId, string message, string messageType = "Text")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId) || 
                    string.IsNullOrWhiteSpace(message))
                {
                    Client.OnChatError("INVALID_PARAMS", "房间ID、玩家ID和消息不能为空");
                    return;
                }

                // 获取玩家信息
                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);
                
                if (playerInfo == null)
                {
                    Client.OnChatError("PLAYER_NOT_FOUND", "玩家不存在");
                    return;
                }

                var messageId = Guid.NewGuid().ToString();
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var senderName = playerInfo.DisplayName ?? playerId;

                _logger.LogInformation("房间聊天消息: RoomId={RoomId}, PlayerId={PlayerId}, MessageType={MessageType}",
                    roomId, playerId, messageType);

                // TODO: 保存聊天消息到持久化存储
                // 可以创建一个ChatGrain来处理聊天消息的持久化

                // 广播聊天消息到房间内所有玩家
                var roomKey = $"room_{roomId}";
                if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
                {
                    // 向房间内所有玩家广播消息
                    roomGroup.All.OnRoomChatMessage(messageId, roomId, playerId, senderName, message, messageType, timestamp);
                    _logger.LogInformation("房间消息已广播: MessageId={MessageId}, RoomId={RoomId}", messageId, roomId);
                }
                else
                {
                    // 如果找不到群组，只通知发送者
                    Client.OnRoomChatMessage(messageId, roomId, playerId, senderName, message, messageType, timestamp);
                    _logger.LogWarning("房间群组不存在，无法广播消息: RoomId={RoomId}", roomId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送房间聊天消息失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
                Client.OnChatError("SEND_MESSAGE_FAILED", "发送消息失败");
            }
        }

        /// <summary>
        /// 获取房间聊天历史
        /// </summary>
        public async ValueTask GetRoomChatHistoryAsync(string roomId, string playerId, int pageIndex = 0, int pageSize = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                {
                    Client.OnChatError("INVALID_PARAMS", "房间ID和玩家ID不能为空");
                    return;
                }

                _logger.LogDebug("获取房间聊天历史: RoomId={RoomId}, PlayerId={PlayerId}, Page={Page}, Size={Size}",
                    roomId, playerId, pageIndex, pageSize);

                // TODO: 从持久化存储获取聊天历史
                // 当前返回空历史
                var emptyHistory = "[]";
                
                Client.OnRoomChatHistory(roomId, emptyHistory, 0, pageIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取房间聊天历史失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
                Client.OnChatError("GET_HISTORY_FAILED", "获取聊天历史失败");
            }
        }

        #endregion

        #region 私人聊天API实现

        /// <summary>
        /// 发送私聊消息
        /// </summary>
        public async ValueTask SendPrivateMessageAsync(string fromPlayerId, string toPlayerId, string message, string messageType = "Text")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fromPlayerId) || string.IsNullOrWhiteSpace(toPlayerId) || 
                    string.IsNullOrWhiteSpace(message))
                {
                    Client.OnChatError("INVALID_PARAMS", "发送者ID、接收者ID和消息不能为空");
                    return;
                }

                // 获取发送者信息
                var fromPlayerGrain = _grainFactory.GetGrain<IPlayerGrain>(fromPlayerId);
                var fromPlayerInfo = await fromPlayerGrain.GetPlayerInfoAsync(false, false);
                
                if (fromPlayerInfo == null)
                {
                    Client.OnChatError("SENDER_NOT_FOUND", "发送者不存在");
                    return;
                }

                var messageId = Guid.NewGuid().ToString();
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var senderName = fromPlayerInfo.DisplayName ?? fromPlayerId;

                _logger.LogInformation("私聊消息: From={FromPlayerId}, To={ToPlayerId}, MessageType={MessageType}",
                    fromPlayerId, toPlayerId, messageType);

                // TODO: 保存私聊消息到持久化存储

                // 发送给接收者（如果在线）
                if (_playerConnections.ContainsKey(toPlayerId))
                {
                    // 需要通过特定连接发送，这里简化处理
                    // 实际应该通过连接ID找到对应的客户端
                }

                // 发送确认给发送者
                Client.OnPrivateMessage(messageId, fromPlayerId, senderName, message, messageType, timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送私聊消息失败: From={FromPlayerId}, To={ToPlayerId}", fromPlayerId, toPlayerId);
                Client.OnChatError("SEND_PRIVATE_MESSAGE_FAILED", "发送私聊消息失败");
            }
        }

        /// <summary>
        /// 获取私聊历史
        /// </summary>
        public async ValueTask GetPrivateChatHistoryAsync(string playerId1, string playerId2, string requesterId, int pageIndex = 0, int pageSize = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId1) || string.IsNullOrWhiteSpace(playerId2) || 
                    string.IsNullOrWhiteSpace(requesterId))
                {
                    Client.OnChatError("INVALID_PARAMS", "参数不能为空");
                    return;
                }

                // 验证请求者是否有权限查看这个聊天历史
                if (requesterId != playerId1 && requesterId != playerId2)
                {
                    Client.OnChatError("NO_PERMISSION", "没有权限查看此聊天历史");
                    return;
                }

                _logger.LogDebug("获取私聊历史: Player1={Player1}, Player2={Player2}, Requester={Requester}",
                    playerId1, playerId2, requesterId);

                // TODO: 从持久化存储获取私聊历史
                var emptyHistory = "[]";
                var chatPartnerId = requesterId == playerId1 ? playerId2 : playerId1;
                
                Client.OnPrivateChatHistory(chatPartnerId, emptyHistory, 0, pageIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取私聊历史失败: Player1={Player1}, Player2={Player2}", playerId1, playerId2);
                Client.OnChatError("GET_PRIVATE_HISTORY_FAILED", "获取私聊历史失败");
            }
        }

        /// <summary>
        /// 标记私聊消息为已读
        /// </summary>
        public async ValueTask MarkPrivateMessageAsReadAsync(string playerId, string fromPlayerId, string messageId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(fromPlayerId) || 
                    string.IsNullOrWhiteSpace(messageId))
                {
                    return;
                }

                _logger.LogDebug("标记私聊消息已读: PlayerId={PlayerId}, FromPlayerId={FromPlayerId}, MessageId={MessageId}",
                    playerId, fromPlayerId, messageId);

                // TODO: 更新消息状态到持久化存储
                
                // 通知发送者消息已读（需要找到发送者的连接）
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记私聊消息已读失败: PlayerId={PlayerId}, MessageId={MessageId}", playerId, messageId);
            }
        }

        #endregion

        #region 其他API简化实现

        public async ValueTask JoinGlobalChannelAsync(string playerId, string channelName)
        {
            try
            {
                var channelKey = $"global_{channelName}";
                var globalGroup = await Group.AddAsync(channelKey);
                _globalGroups.AddOrUpdate(channelKey, globalGroup, (key, oldGroup) => globalGroup);
                
                _globalChannels.AddOrUpdate(channelKey, new HashSet<string> { playerId },
                    (key, existingSet) =>
                    {
                        lock (existingSet)
                        {
                            existingSet.Add(playerId);
                            return existingSet;
                        }
                    });

                var onlineCount = _globalChannels.ContainsKey(channelKey) ? _globalChannels[channelKey].Count : 0;
                Client.OnGlobalChannelStatusUpdate(channelName, onlineCount, true);
                
                _logger.LogInformation("玩家加入全局频道: PlayerId={PlayerId}, Channel={Channel}", playerId, channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加入全局频道失败: PlayerId={PlayerId}, Channel={Channel}", playerId, channelName);
            }
        }

        public async ValueTask LeaveGlobalChannelAsync(string playerId, string channelName)
        {
            try
            {
                var channelKey = $"global_{channelName}";
                
                // 从全局频道群组中正确移除
                if (_globalGroups.TryGetValue(channelKey, out var globalGroup))
                {
                    await globalGroup.RemoveAsync(Context);
                }
                if (_globalChannels.TryGetValue(channelKey, out var playerSet))
                {
                    lock (playerSet)
                    {
                        playerSet.Remove(playerId);
                        if (playerSet.Count == 0)
                        {
                            _globalChannels.TryRemove(channelKey, out _);
                        }
                    }
                }

                var onlineCount = _globalChannels.ContainsKey(channelKey) ? _globalChannels[channelKey].Count : 0;
                Client.OnGlobalChannelStatusUpdate(channelName, onlineCount, false);
                
                _logger.LogInformation("玩家离开全局频道: PlayerId={PlayerId}, Channel={Channel}", playerId, channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "离开全局频道失败: PlayerId={PlayerId}, Channel={Channel}", playerId, channelName);
            }
        }

        public async ValueTask SendGlobalChannelMessageAsync(string playerId, string channelName, string message, string messageType = "Text")
        {
            try
            {
                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);
                
                var messageId = Guid.NewGuid().ToString();
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var senderName = playerInfo?.DisplayName ?? playerId;

                // 广播消息到全局频道内所有玩家
                var channelKey = $"global_{channelName}";
                if (_globalGroups.TryGetValue(channelKey, out var globalGroup))
                {
                    globalGroup.All.OnGlobalChannelMessage(messageId, channelName, playerId, senderName, message, messageType, timestamp);
                    _logger.LogInformation("全局频道消息已广播: MessageId={MessageId}, Channel={Channel}", messageId, channelName);
                }
                else
                {
                    Client.OnGlobalChannelMessage(messageId, channelName, playerId, senderName, message, messageType, timestamp);
                    _logger.LogWarning("全局频道群组不存在: Channel={Channel}", channelName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送全局频道消息失败: PlayerId={PlayerId}, Channel={Channel}", playerId, channelName);
            }
        }

        // 简化实现其他方法
        public async ValueTask DeleteMessageAsync(string messageId, string requesterId) => 
            Client.OnMessageDeleted(messageId, requesterId, "用户删除");

        public async ValueTask ReportMessageAsync(string messageId, string reporterId, string reason) => 
            _logger.LogInformation("消息举报: MessageId={MessageId}, ReporterId={ReporterId}, Reason={Reason}", messageId, reporterId, reason);

        public async ValueTask BlockPlayerAsync(string playerId, string blockedPlayerId) => 
            Client.OnPlayerBlockStatusUpdate(blockedPlayerId, true);

        public async ValueTask UnblockPlayerAsync(string playerId, string unblockedPlayerId) => 
            Client.OnPlayerBlockStatusUpdate(unblockedPlayerId, false);

        public async ValueTask SendSystemNotificationAsync(string targetType, string targetId, string notificationType, string title, string content) { }

        public async ValueTask<int> GetUnreadNotificationCountAsync(string playerId) => 0;

        public async ValueTask MarkNotificationAsReadAsync(string playerId, string notificationId) { }

        public async ValueTask SendEmojiReactionAsync(string messageId, string playerId, string emojiCode) { }

        public async ValueTask SendVoiceMessageAsync(string? roomId, string fromPlayerId, string? toPlayerId, string voiceMessageId, int duration) { }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从所有频道移除玩家
        /// </summary>
        private void RemovePlayerFromAllChannels(string playerId)
        {
            // 从房间频道移除
            foreach (var roomChannel in _roomChannels.ToList())
            {
                if (roomChannel.Value != null)
                {
                    lock (roomChannel.Value)
                    {
                        roomChannel.Value.Remove(playerId);
                        if (roomChannel.Value.Count == 0)
                        {
                            _roomChannels.TryRemove(roomChannel.Key, out _);
                        }
                    }
                }
            }

            // 从全局频道移除
            foreach (var globalChannel in _globalChannels.ToList())
            {
                if (globalChannel.Value != null)
                {
                    lock (globalChannel.Value)
                    {
                        globalChannel.Value.Remove(playerId);
                        if (globalChannel.Value.Count == 0)
                        {
                            _globalChannels.TryRemove(globalChannel.Key, out _);
                        }
                    }
                }
            }
        }

        #endregion
    }
}