using MagicOnion.Server.Hubs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Wind.Shared.Services;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.GrainInterfaces;
using Orleans;
using MagicOnion.Server;

namespace Wind.Server.Services
{
    /// <summary>
    /// 玩家StreamingHub实现 - 处理实时双向通信
    /// 支持玩家连接状态管理、实时消息推送、房间内状态同步等功能
    /// </summary>
    public class PlayerHub : StreamingHubBase<IPlayerHub, IPlayerHubReceiver>, IPlayerHub
    {
        private readonly ILogger<PlayerHub> _logger;
        private readonly IGrainFactory _grainFactory;
        private readonly RoomStateBroadcaster _roomBroadcaster;
        
        // 连接状态管理
        private string? _playerId;
        private IPlayerGrain? _playerGrain;
        private bool _isAuthenticated = false;
        
        // 房间群组管理
        private IGroup<IPlayerHubReceiver>? _currentRoom;
        private string? _currentRoomId;
        
        public PlayerHub(ILogger<PlayerHub> logger, IGrainFactory grainFactory, RoomStateBroadcaster roomBroadcaster)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
            _roomBroadcaster = roomBroadcaster ?? throw new ArgumentNullException(nameof(roomBroadcaster));
        }
        
        #region StreamingHub 生命周期事件
        
        /// <summary>
        /// 客户端连接建立时调用
        /// </summary>
        protected override async ValueTask OnConnected()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", ConnectionId);
            
            try
            {
                // 发送连接成功通知给客户端
                Client.OnConnected("", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                _logger.LogDebug("Connection established notification sent to client: {ConnectionId}", ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OnConnected for {ConnectionId}", ConnectionId);
            }
            
            await ValueTask.CompletedTask;
        }
        
        /// <summary>
        /// 客户端连接断开时调用
        /// </summary>
        protected override async ValueTask OnDisconnected()
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}, PlayerId: {PlayerId}", 
                ConnectionId, _playerId);
            
            try
            {
                // 如果玩家已认证，执行下线清理
                if (_isAuthenticated && !string.IsNullOrEmpty(_playerId) && _playerGrain != null)
                {
                    // 更新玩家离线状态
                    await _playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Offline);
                    
                    // 离开当前房间群组
                    if (_currentRoom != null && !string.IsNullOrEmpty(_currentRoomId))
                    {
                        await _currentRoom.RemoveAsync(Context);
                        
                        // 通知房间内其他玩家该玩家离开
                        _currentRoom.All.OnPlayerLeftRoom(_currentRoomId, _playerId, "玩家");
                        _logger.LogDebug("Player {PlayerId} left room {RoomId} due to disconnection", _playerId, _currentRoomId);
                    }
                    
                    _logger.LogInformation("Player {PlayerId} offline cleanup completed", _playerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OnDisconnected cleanup for {ConnectionId}, PlayerId: {PlayerId}", 
                    ConnectionId, _playerId);
            }
            
            await ValueTask.CompletedTask;
        }
        
        #endregion
        
        #region 连接管理实现
        
        /// <summary>
        /// 玩家上线通知 - 建立Hub连接后调用
        /// </summary>
        public async ValueTask OnlineAsync(string playerId, string accessToken)
        {
            try
            {
                _logger.LogInformation("Player online request: {PlayerId}, ConnectionId: {ConnectionId}", 
                    playerId, ConnectionId);
                
                // TODO: 验证JWT令牌有效性
                // 当前简化处理，生产环境需要验证accessToken
                
                if (string.IsNullOrEmpty(playerId))
                {
                    Client.OnError("INVALID_PLAYER_ID", "玩家ID不能为空");
                    return;
                }
                
                // 获取PlayerGrain实例
                _playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                
                // 更新玩家在线状态
                await _playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Online);
                
                // 设置连接状态
                _playerId = playerId;
                _isAuthenticated = true;
                
                // 通知客户端上线成功
                Client.OnConnected(playerId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                
                _logger.LogInformation("Player {PlayerId} online successful", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during player online: {PlayerId}", playerId);
                Client.OnError("ONLINE_FAILED", $"上线失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 玩家下线通知 - 主动断开连接前调用
        /// </summary>
        public async ValueTask OfflineAsync(string playerId)
        {
            try
            {
                _logger.LogInformation("Player offline request: {PlayerId}", playerId);
                
                if (_playerId != playerId)
                {
                    Client.OnError("INVALID_PLAYER", "玩家ID不匹配");
                    return;
                }
                
                if (_playerGrain != null)
                {
                    // 更新玩家离线状态
                    await _playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Offline);
                }
                
                // 清理连接状态
                _isAuthenticated = false;
                _playerId = null;
                _playerGrain = null;
                
                // 通知客户端下线完成
                Client.OnDisconnected("USER_REQUESTED");
                
                _logger.LogInformation("Player {PlayerId} offline successful", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during player offline: {PlayerId}", playerId);
                Client.OnError("OFFLINE_FAILED", $"下线失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 心跳检测 - 保持连接活跃状态
        /// </summary>
        public async ValueTask<long> HeartbeatAsync()
        {
            try
            {
                var serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                // 发送心跳响应给客户端
                Client.OnHeartbeatResponse(serverTime);
                
                _logger.LogDebug("Heartbeat from {PlayerId}, ConnectionId: {ConnectionId}", 
                    _playerId, ConnectionId);
                
                return await ValueTask.FromResult(serverTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during heartbeat for {PlayerId}", _playerId);
                throw;
            }
        }
        
        #endregion
        
        #region 房间相关实现
        
        /// <summary>
        /// 加入房间实时通信群组
        /// </summary>
        public async ValueTask JoinRoomAsync(string roomId, string playerId)
        {
            try
            {
                _logger.LogInformation("Join room request: PlayerId: {PlayerId}, RoomId: {RoomId}", playerId, roomId);
                
                if (!_isAuthenticated || _playerId != playerId)
                {
                    Client.OnError("NOT_AUTHENTICATED", "未认证或玩家ID不匹配");
                    return;
                }
                
                if (string.IsNullOrEmpty(roomId))
                {
                    Client.OnError("INVALID_ROOM_ID", "房间ID不能为空");
                    return;
                }
                
                // 先离开当前房间（如果有）
                if (_currentRoom != null)
                {
                    await _currentRoom.RemoveAsync(Context);
                }
                
                // 加入新房间群组
                _currentRoom = await Group.AddAsync(roomId);
                _currentRoomId = roomId;
                
                // 通知房间内其他玩家有新玩家加入
                _currentRoom.All.OnPlayerJoinedRoom(roomId, playerId, "玩家");
                
                // 获取房间信息（从RoomGrain）
                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                // TODO: 获取房间详细信息，当前简化处理
                
                // 通知客户端加入房间成功
                Client.OnRoomJoined(roomId, $"Room_{roomId}", 1); // TODO: 获取实际玩家数
                
                _logger.LogInformation("Player {PlayerId} joined room {RoomId} successfully", playerId, roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining room: PlayerId: {PlayerId}, RoomId: {RoomId}", playerId, roomId);
                Client.OnError("JOIN_ROOM_FAILED", $"加入房间失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 离开房间实时通信群组
        /// </summary>
        public async ValueTask LeaveRoomAsync(string roomId, string playerId)
        {
            try
            {
                _logger.LogInformation("Leave room request: PlayerId: {PlayerId}, RoomId: {RoomId}", playerId, roomId);
                
                if (!_isAuthenticated || _playerId != playerId)
                {
                    Client.OnError("NOT_AUTHENTICATED", "未认证或玩家ID不匹配");
                    return;
                }
                
                if (_currentRoom != null && _currentRoomId == roomId)
                {
                    // 通知房间内其他玩家该玩家离开
                    _currentRoom.All.OnPlayerLeftRoom(roomId, playerId, "玩家");
                    
                    // 离开房间群组
                    await _currentRoom.RemoveAsync(Context);
                    _currentRoom = null;
                    _currentRoomId = null;
                    
                    // 通知客户端离开房间成功
                    Client.OnRoomLeft(roomId, "USER_REQUESTED");
                    
                    _logger.LogInformation("Player {PlayerId} left room {RoomId} successfully", playerId, roomId);
                }
                else
                {
                    Client.OnError("NOT_IN_ROOM", "当前不在指定房间中");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving room: PlayerId: {PlayerId}, RoomId: {RoomId}", playerId, roomId);
                Client.OnError("LEAVE_ROOM_FAILED", $"离开房间失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 广播玩家在房间内的状态更新
        /// </summary>
        public async ValueTask UpdatePlayerStatusAsync(string playerId, string newStatus)
        {
            try
            {
                _logger.LogDebug("Update player status: PlayerId: {PlayerId}, Status: {Status}", playerId, newStatus);
                
                if (!_isAuthenticated || _playerId != playerId)
                {
                    Client.OnError("NOT_AUTHENTICATED", "未认证或玩家ID不匹配");
                    return;
                }
                
                if (_currentRoom != null)
                {
                    // 广播状态更新给房间内所有玩家
                    _currentRoom.All.OnPlayerStatusChanged(playerId, "玩家", newStatus);
                }
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player status: PlayerId: {PlayerId}", playerId);
                Client.OnError("UPDATE_STATUS_FAILED", $"更新状态失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 广播玩家位置信息更新
        /// </summary>
        public async ValueTask UpdatePlayerPositionAsync(string playerId, float x, float y, float z)
        {
            try
            {
                _logger.LogTrace("Update player position: PlayerId: {PlayerId}, Position: ({X}, {Y}, {Z})", 
                    playerId, x, y, z);
                
                if (!_isAuthenticated || _playerId != playerId)
                {
                    Client.OnError("NOT_AUTHENTICATED", "未认证或玩家ID不匹配");
                    return;
                }
                
                if (_currentRoom != null)
                {
                    // 使用RoomStateBroadcaster进行增强的位置广播
                    var position = new PlayerPosition 
                    { 
                        X = x, 
                        Y = y, 
                        Z = z, 
                        UpdatedAt = DateTime.UtcNow 
                    };
                    
                    await _roomBroadcaster.BroadcastPlayerPositionUpdate(
                        _currentRoom, playerId, position, new[] { ConnectionId });
                    
                    // 同时更新PlayerGrain中的位置信息
                    if (_playerGrain != null)
                    {
                        try
                        {
                            await _playerGrain.UpdatePositionAsync(position);
                        }
                        catch (Exception grainEx)
                        {
                            _logger.LogWarning(grainEx, "更新PlayerGrain位置信息失败: PlayerId={PlayerId}", playerId);
                        }
                    }
                }
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player position: PlayerId: {PlayerId}", playerId);
                Client.OnError("UPDATE_POSITION_FAILED", $"更新位置失败: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 实时消息实现
        
        /// <summary>
        /// 发送房间聊天消息
        /// </summary>
        public async ValueTask SendRoomMessageAsync(string roomId, string playerId, string message)
        {
            try
            {
                _logger.LogDebug("Send room message: PlayerId: {PlayerId}, RoomId: {RoomId}, Message: {Message}", 
                    playerId, roomId, message);
                
                if (!_isAuthenticated || _playerId != playerId)
                {
                    Client.OnError("NOT_AUTHENTICATED", "未认证或玩家ID不匹配");
                    return;
                }
                
                if (_currentRoom != null && _currentRoomId == roomId)
                {
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    
                    // 广播消息给房间内所有玩家
                    _currentRoom.All.OnRoomMessage(roomId, playerId, "玩家", message, timestamp);
                }
                else
                {
                    Client.OnError("NOT_IN_ROOM", "当前不在指定房间中");
                }
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending room message: PlayerId: {PlayerId}", playerId);
                Client.OnError("SEND_MESSAGE_FAILED", $"发送消息失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送私聊消息
        /// </summary>
        public async ValueTask SendPrivateMessageAsync(string fromPlayerId, string toPlayerId, string message)
        {
            try
            {
                _logger.LogDebug("Send private message: From: {FromPlayerId}, To: {ToPlayerId}", fromPlayerId, toPlayerId);
                
                if (!_isAuthenticated || _playerId != fromPlayerId)
                {
                    Client.OnError("NOT_AUTHENTICATED", "未认证或玩家ID不匹配");
                    return;
                }
                
                // TODO: 实现私聊消息路由
                // 需要查找目标玩家的连接并发送消息
                // 当前简化处理，标记为待实现功能
                
                Client.OnWarning("FEATURE_NOT_IMPLEMENTED", "私聊功能暂未实现，需要消息路由系统支持");
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending private message: From: {FromPlayerId}, To: {ToPlayerId}", 
                    fromPlayerId, toPlayerId);
                Client.OnError("SEND_PRIVATE_MESSAGE_FAILED", $"发送私聊失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送系统通知
        /// </summary>
        public async ValueTask SendSystemNotificationAsync(string playerId, string notificationType, string content)
        {
            try
            {
                _logger.LogDebug("Send system notification: PlayerId: {PlayerId}, Type: {Type}", playerId, notificationType);
                
                // 系统通知不需要验证发送者身份
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                // 发送系统通知给指定客户端
                Client.OnSystemNotification(notificationType, "系统通知", content, timestamp);
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system notification: PlayerId: {PlayerId}", playerId);
                Client.OnError("SEND_NOTIFICATION_FAILED", $"发送通知失败: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 匹配相关实现
        
        /// <summary>
        /// 加入匹配队列通知
        /// </summary>
        public async ValueTask JoinMatchmakingAsync(string playerId, string matchmakingRequest)
        {
            try
            {
                _logger.LogDebug("Join matchmaking: PlayerId: {PlayerId}", playerId);
                
                if (!_isAuthenticated || _playerId != playerId)
                {
                    Client.OnError("NOT_AUTHENTICATED", "未认证或玩家ID不匹配");
                    return;
                }
                
                // TODO: 集成MatchmakingGrain
                // 当前简化处理，标记为待实现功能
                
                Client.OnMatchmakingJoined("queue_001", 30); // 模拟匹配队列
                Client.OnWarning("FEATURE_PARTIAL", "匹配系统部分功能待实现，需要MatchmakingGrain集成");
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining matchmaking: PlayerId: {PlayerId}", playerId);
                Client.OnError("JOIN_MATCHMAKING_FAILED", $"加入匹配失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 离开匹配队列通知
        /// </summary>
        public async ValueTask LeaveMatchmakingAsync(string playerId)
        {
            try
            {
                _logger.LogDebug("Leave matchmaking: PlayerId: {PlayerId}", playerId);
                
                if (!_isAuthenticated || _playerId != playerId)
                {
                    Client.OnError("NOT_AUTHENTICATED", "未认证或玩家ID不匹配");
                    return;
                }
                
                // TODO: 集成MatchmakingGrain
                Client.OnMatchmakingCancelled("USER_REQUESTED");
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving matchmaking: PlayerId: {PlayerId}", playerId);
                Client.OnError("LEAVE_MATCHMAKING_FAILED", $"离开匹配失败: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 游戏内事件实现
        
        /// <summary>
        /// 玩家准备状态更新
        /// </summary>
        public async ValueTask SetReadyStatusAsync(string playerId, bool isReady)
        {
            try
            {
                _logger.LogDebug("Set ready status: PlayerId: {PlayerId}, IsReady: {IsReady}", playerId, isReady);
                
                if (!_isAuthenticated || _playerId != playerId)
                {
                    Client.OnError("NOT_AUTHENTICATED", "未认证或玩家ID不匹配");
                    return;
                }
                
                if (_currentRoom != null && !string.IsNullOrEmpty(_currentRoomId))
                {
                    // 创建RoomPlayer对象用于广播
                    var roomPlayer = new RoomPlayer
                    {
                        PlayerId = playerId,
                        DisplayName = "玩家", // TODO: 从PlayerGrain获取真实姓名
                        ReadyStatus = isReady ? PlayerReadyStatus.Ready : PlayerReadyStatus.NotReady
                    };
                    
                    // 使用RoomStateBroadcaster进行增强的准备状态广播
                    await _roomBroadcaster.BroadcastPlayerReadyStatusChanged(_currentRoom, roomPlayer);
                    
                    // 同时通知RoomGrain更新玩家准备状态
                    if (!string.IsNullOrEmpty(_currentRoomId))
                    {
                        try
                        {
                            var roomGrain = _grainFactory.GetGrain<IRoomGrain>(_currentRoomId);
                            await roomGrain.SetPlayerReadyAsync(new PlayerReadyRequest 
                            { 
                                RoomId = _currentRoomId,
                                PlayerId = playerId, 
                                ReadyStatus = isReady ? PlayerReadyStatus.Ready : PlayerReadyStatus.NotReady 
                            });
                        }
                        catch (Exception grainEx)
                        {
                            _logger.LogWarning(grainEx, "更新RoomGrain玩家准备状态失败: PlayerId={PlayerId}, RoomId={RoomId}", 
                                playerId, _currentRoomId);
                        }
                    }
                }
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting ready status: PlayerId: {PlayerId}", playerId);
                Client.OnError("SET_READY_FAILED", $"设置准备状态失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 游戏开始事件
        /// </summary>
        public async ValueTask GameStartAsync(string roomId)
        {
            try
            {
                _logger.LogInformation("Game start: RoomId: {RoomId}", roomId);
                
                if (_currentRoom != null && _currentRoomId == roomId)
                {
                    // 从RoomGrain获取完整房间状态
                    try
                    {
                        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                        var roomInfoResponse = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
                        var roomState = roomInfoResponse?.RoomInfo;
                        
                        if (roomState != null)
                        {
                            // 使用RoomStateBroadcaster进行增强的游戏开始广播
                            await _roomBroadcaster.BroadcastGameStarted(_currentRoom, roomState);
                            
                            // 同时通知RoomGrain游戏开始
                            await roomGrain.StartGameAsync(new StartGameRequest { RoomId = roomId });
                        }
                        else
                        {
                            // 如果无法获取房间状态，使用简化广播
                            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            var gameSettings = "{\"mode\":\"standard\",\"duration\":600}";
                            _currentRoom.All.OnGameStart(roomId, gameSettings, timestamp);
                        }
                    }
                    catch (Exception grainEx)
                    {
                        _logger.LogWarning(grainEx, "无法从RoomGrain获取状态，使用简化广播: RoomId={RoomId}", roomId);
                        
                        // 降级处理：直接广播游戏开始
                        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var gameSettings = "{\"mode\":\"standard\",\"duration\":600}";
                        _currentRoom.All.OnGameStart(roomId, gameSettings, timestamp);
                    }
                }
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting game: RoomId: {RoomId}", roomId);
                Client.OnError("GAME_START_FAILED", $"游戏开始失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 游戏结束事件
        /// </summary>
        public async ValueTask GameEndAsync(string roomId, string gameResult)
        {
            try
            {
                _logger.LogInformation("Game end: RoomId: {RoomId}", roomId);
                
                if (_currentRoom != null && _currentRoomId == roomId)
                {
                    try
                    {
                        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                        var roomInfoResponse = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
                        var roomState = roomInfoResponse?.RoomInfo;
                        
                        if (roomState != null)
                        {
                            // 解析游戏结果
                            var gameResultDict = new Dictionary<string, object>();
                            try
                            {
                                gameResultDict = JsonSerializer.Deserialize<Dictionary<string, object>>(gameResult) ?? new();
                            }
                            catch
                            {
                                gameResultDict["result"] = gameResult;
                            }
                            
                            // 使用RoomStateBroadcaster进行增强的游戏结束广播
                            await _roomBroadcaster.BroadcastGameEnded(_currentRoom, roomState, gameResultDict);
                            
                            // 同时通知RoomGrain游戏结束
                            await roomGrain.EndGameAsync(new EndGameRequest 
                            { 
                                RoomId = roomId,
                                PlayerId = Context.ContextId.ToString(), // 使用当前连接的玩家ID
                                FinalScores = gameResultDict.ContainsKey("scores") ? 
                                    (Dictionary<string, int>)(gameResultDict["scores"] ?? new Dictionary<string, int>()) :
                                    new Dictionary<string, int>()
                            });
                        }
                        else
                        {
                            // 降级处理：直接广播游戏结束
                            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            _currentRoom.All.OnGameEnd(roomId, gameResult, timestamp);
                        }
                    }
                    catch (Exception grainEx)
                    {
                        _logger.LogWarning(grainEx, "无法从RoomGrain获取状态，使用简化广播: RoomId={RoomId}", roomId);
                        
                        // 降级处理：直接广播游戏结束
                        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        _currentRoom.All.OnGameEnd(roomId, gameResult, timestamp);
                    }
                }
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending game: RoomId: {RoomId}", roomId);
                Client.OnError("GAME_END_FAILED", $"游戏结束失败: {ex.Message}");
            }
        }
        
        #endregion
    }
}