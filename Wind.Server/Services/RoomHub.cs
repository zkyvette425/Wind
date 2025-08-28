using MagicOnion.Server.Hubs;
using Microsoft.Extensions.Logging;
using Orleans;
using System.Collections.Concurrent;
using Wind.GrainInterfaces;
using Wind.Shared.Services;
using Wind.Server.Services;
using System.Text.Json;

namespace Wind.Server.Services
{
    /// <summary>
    /// 房间StreamingHub实现
    /// 提供房间实时状态同步和游戏事件推送功能
    /// 与Orleans RoomGrain集成，支持分布式房间管理
    /// </summary>
    public class RoomHub : StreamingHubBase<IRoomHub, IRoomHubReceiver>, IRoomHub
    {
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger<RoomHub> _logger;
        private readonly JwtService _jwtService;

        // 连接管理
        private readonly ConcurrentDictionary<string, string> _playerConnections = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _roomConnections = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _observerConnections = new();
        
        // MagicOnion Group管理 - 保存Group引用以便广播
        private readonly ConcurrentDictionary<string, IGroup<IRoomHubReceiver>> _roomGroups = new();
        private readonly ConcurrentDictionary<string, IGroup<IRoomHubReceiver>> _observerGroups = new();

        public RoomHub(IGrainFactory grainFactory, ILogger<RoomHub> logger, JwtService jwtService)
        {
            _grainFactory = grainFactory;
            _logger = logger;
            _jwtService = jwtService;
        }

        #region 连接生命周期

        /// <summary>
        /// 客户端连接时触发
        /// </summary>
        protected override ValueTask OnConnecting()
        {
            _logger.LogInformation("房间Hub连接建立中: ConnectionId={ConnectionId}", ConnectionId);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 客户端断开连接时触发
        /// </summary>
        protected override async ValueTask OnDisconnected()
        {
            var connectionId = ConnectionId.ToString();
            _logger.LogInformation("房间Hub连接断开: ConnectionId={ConnectionId}", connectionId);

            // 清理连接信息
            var playersToRemove = _playerConnections
                .Where(pair => pair.Value == connectionId)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var playerId in playersToRemove)
            {
                _playerConnections.TryRemove(playerId, out _);
                
                // 从所有房间群组中移除并广播离开事件
                await RemovePlayerFromAllRoomsWithBroadcast(playerId);
                
                _logger.LogInformation("房间Hub断开连接清理: PlayerId={PlayerId}", playerId);
            }
        }

        #endregion

        #region 房间连接管理API实现

        /// <summary>
        /// 连接房间Hub服务
        /// </summary>
        public async ValueTask ConnectToRoomAsync(string playerId, string roomId, string accessToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(roomId) || 
                    string.IsNullOrWhiteSpace(accessToken))
                {
                    Client.OnRoomError(roomId, "INVALID_PARAMS", "参数不能为空");
                    return;
                }

                // 验证JWT令牌
                var validationResult = _jwtService.ValidateAccessToken(accessToken);
                if (!validationResult.IsValid)
                {
                    Client.OnRoomError(roomId, "INVALID_TOKEN", "访问令牌无效或已过期");
                    return;
                }

                // 验证令牌中的玩家ID
                var tokenPlayerId = _jwtService.ExtractPlayerIdFromToken(accessToken);
                if (tokenPlayerId != playerId)
                {
                    Client.OnRoomError(roomId, "TOKEN_PLAYER_MISMATCH", "令牌中的玩家ID与请求的不匹配");
                    return;
                }

                // 验证玩家是否在房间中
                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var currentRoomId = await playerGrain.GetCurrentRoomAsync();
                
                if (currentRoomId != roomId)
                {
                    Client.OnRoomError(roomId, "NOT_IN_ROOM", "玩家不在指定房间中");
                    return;
                }

                _logger.LogInformation("房间Hub连接成功: PlayerId={PlayerId}, RoomId={RoomId}, ConnectionId={ConnectionId}",
                    playerId, roomId, ConnectionId);

                // 记录连接
                _playerConnections.AddOrUpdate(playerId, ConnectionId.ToString(), (key, oldValue) => ConnectionId.ToString());

                // 加入房间群组并保存引用
                var roomGroup = await Group.AddAsync($"room_{roomId}");
                var roomKey = $"room_{roomId}";
                _roomGroups.AddOrUpdate(roomKey, roomGroup, (key, oldGroup) => roomGroup);
                
                // 记录到房间连接
                _roomConnections.AddOrUpdate(roomKey, new HashSet<string> { playerId }, 
                    (key, existingSet) => 
                    {
                        lock (existingSet)
                        {
                            existingSet.Add(playerId);
                            return existingSet;
                        }
                    });

                // 更新玩家在线状态
                await playerGrain.HeartbeatAsync();

                // 通知客户端连接成功
                Client.OnRoomHubConnected(playerId, roomId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                // 通知房间内其他玩家有新玩家连接
                var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);
                var playerName = playerInfo?.DisplayName ?? playerId;
                var playerData = JsonSerializer.Serialize(new { playerId, playerName, status = "connected" });

                // 向房间内其他玩家广播新玩家连接事件（排除自己）
                roomGroup.Except(new[] { ConnectionId }).OnPlayerJoinedRoom(roomId, playerId, playerName, playerData);
                
                // 向自己发送房间连接成功确认
                Client.OnPlayerJoinedRoom(roomId, playerId, playerName, playerData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "房间Hub连接失败: PlayerId={PlayerId}, RoomId={RoomId}", playerId, roomId);
                Client.OnRoomError(roomId, "CONNECTION_FAILED", "房间Hub连接失败");
            }
        }

        /// <summary>
        /// 断开房间Hub连接
        /// </summary>
        public async ValueTask DisconnectFromRoomAsync(string playerId, string roomId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(roomId))
                {
                    return;
                }

                _logger.LogInformation("房间Hub主动断开: PlayerId={PlayerId}, RoomId={RoomId}", playerId, roomId);

                // 从连接记录中移除
                if (_playerConnections.TryRemove(playerId, out var connectionId))
                {
                    var roomKey = $"room_{roomId}";
                    
                    // 从房间群组中正确移除
                    if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
                    {
                        await roomGroup.RemoveAsync(Context);
                    }
                    
                    // 从房间连接中移除
                    RemovePlayerFromRoom(playerId, roomId);
                    
                    // 获取玩家信息用于通知
                    try
                    {
                        var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                        var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);
                        var playerName = playerInfo?.DisplayName ?? playerId;

                        // 向房间内其他玩家广播玩家离开事件
                        if (_roomGroups.TryGetValue(roomKey, out var broadcastGroup))
                        {
                            broadcastGroup.All.OnPlayerLeftRoom(roomId, playerId, playerName, "主动断开");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "获取玩家信息失败，使用默认信息: PlayerId={PlayerId}", playerId);
                        // 向房间内其他玩家广播玩家离开事件（使用默认信息）
                        if (_roomGroups.TryGetValue(roomKey, out var broadcastGroup))
                        {
                            broadcastGroup.All.OnPlayerLeftRoom(roomId, playerId, playerId, "主动断开");
                        }
                    }
                    
                    // 通知客户端断开
                    Client.OnRoomHubDisconnected(roomId, "主动断开");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "断开房间Hub失败: PlayerId={PlayerId}, RoomId={RoomId}", playerId, roomId);
            }
        }

        /// <summary>
        /// 房间心跳检测
        /// </summary>
        public async ValueTask<long> RoomHeartbeatAsync(string playerId, string roomId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(roomId))
                {
                    return 0;
                }

                // 更新玩家心跳
                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                await playerGrain.HeartbeatAsync();

                var serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Client.OnRoomHeartbeatResponse(roomId, serverTime);
                
                return serverTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "房间心跳失败: PlayerId={PlayerId}, RoomId={RoomId}", playerId, roomId);
                return 0;
            }
        }

        #endregion

        #region 房间状态同步API实现

        /// <summary>
        /// 更新房间设置
        /// </summary>
        public async ValueTask UpdateRoomSettingsAsync(string roomId, string ownerId, string settingsJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(ownerId))
                {
                    Client.OnRoomError(roomId, "INVALID_PARAMS", "房间ID和房主ID不能为空");
                    return;
                }

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                
                // 检查权限
                var hasPermission = await roomGrain.HasPermissionAsync(ownerId, RoomOperation.UpdateSettings);
                if (!hasPermission)
                {
                    Client.OnRoomError(roomId, "NO_PERMISSION", "没有权限更新房间设置");
                    return;
                }

                _logger.LogInformation("更新房间设置: RoomId={RoomId}, OwnerId={OwnerId}", roomId, ownerId);

                // 广播设置更新到房间内所有玩家
                var roomKey = $"room_{roomId}";
                if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
                {
                    roomGroup.All.OnRoomSettingsUpdate(roomId, settingsJson, ownerId);
                }
                else
                {
                    // 如果找不到群组，只通知当前客户端
                    Client.OnRoomSettingsUpdate(roomId, settingsJson, ownerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新房间设置失败: RoomId={RoomId}, OwnerId={OwnerId}", roomId, ownerId);
                Client.OnRoomError(roomId, "UPDATE_SETTINGS_FAILED", "更新房间设置失败");
            }
        }

        /// <summary>
        /// 更新房间状态
        /// </summary>
        public async ValueTask UpdateRoomStatusAsync(string roomId, string newStatus, string updaterId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(newStatus))
                {
                    Client.OnRoomError(roomId, "INVALID_PARAMS", "房间ID和状态不能为空");
                    return;
                }

                _logger.LogInformation("更新房间状态: RoomId={RoomId}, NewStatus={NewStatus}, UpdaterId={UpdaterId}",
                    roomId, newStatus, updaterId);

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // 广播状态更新到房间内所有玩家
                var roomKey = $"room_{roomId}";
                if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
                {
                    roomGroup.All.OnRoomStatusUpdate(roomId, newStatus, updaterId, timestamp);
                }
                else
                {
                    // 如果找不到群组，只通知当前客户端
                    Client.OnRoomStatusUpdate(roomId, newStatus, updaterId, timestamp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新房间状态失败: RoomId={RoomId}, NewStatus={NewStatus}", roomId, newStatus);
                Client.OnRoomError(roomId, "UPDATE_STATUS_FAILED", "更新房间状态失败");
            }
        }

        /// <summary>
        /// 获取完整房间状态
        /// </summary>
        public async ValueTask RequestFullRoomStateAsync(string roomId, string requesterId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(requesterId))
                {
                    Client.OnRoomError(roomId, "INVALID_PARAMS", "房间ID和请求者ID不能为空");
                    return;
                }

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                
                // 获取房间信息
                var roomInfoRequest = new Wind.Shared.Protocols.GetRoomInfoRequest
                {
                    RoomId = roomId,
                    IncludePlayerDetails = true
                };
                
                var roomInfoResponse = await roomGrain.GetRoomInfoAsync(roomInfoRequest);
                
                if (roomInfoResponse.Success && roomInfoResponse.RoomInfo != null)
                {
                    var roomStateJson = JsonSerializer.Serialize(roomInfoResponse.RoomInfo);
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    
                    Client.OnFullRoomState(roomId, roomStateJson, timestamp);
                    
                    _logger.LogDebug("返回完整房间状态: RoomId={RoomId}, RequesterId={RequesterId}", roomId, requesterId);
                }
                else
                {
                    Client.OnRoomError(roomId, "ROOM_NOT_FOUND", "房间不存在或获取状态失败");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取完整房间状态失败: RoomId={RoomId}, RequesterId={RequesterId}", roomId, requesterId);
                Client.OnRoomError(roomId, "GET_ROOM_STATE_FAILED", "获取房间状态失败");
            }
        }

        #endregion

        #region 玩家状态同步API实现

        /// <summary>
        /// 更新玩家准备状态
        /// </summary>
        public async ValueTask UpdatePlayerReadyStatusAsync(string roomId, string playerId, bool isReady)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                {
                    Client.OnRoomError(roomId, "INVALID_PARAMS", "房间ID和玩家ID不能为空");
                    return;
                }

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);
                var playerName = playerInfo?.DisplayName ?? playerId;

                _logger.LogInformation("更新玩家准备状态: RoomId={RoomId}, PlayerId={PlayerId}, IsReady={IsReady}",
                    roomId, playerId, isReady);

                // 广播到房间内所有玩家
                // Simplified: Just notify the client
                Client.OnPlayerReadyStatusUpdate(roomId, playerId, playerName, isReady);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新玩家准备状态失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
                Client.OnRoomError(roomId, "UPDATE_READY_STATUS_FAILED", "更新准备状态失败");
            }
        }

        /// <summary>
        /// 更新玩家位置信息
        /// </summary>
        public async ValueTask UpdatePlayerPositionAsync(string roomId, string playerId, string positionData)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                {
                    return;
                }

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // 广播位置更新到房间内所有玩家（除了自己）
                // Simplified: Just notify the client
                Client.OnPlayerPositionUpdate(roomId, playerId, positionData, timestamp);
                
                _logger.LogDebug("更新玩家位置: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新玩家位置失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
        }

        /// <summary>
        /// 更新玩家游戏状态
        /// </summary>
        public async ValueTask UpdatePlayerGameStateAsync(string roomId, string playerId, string gameState)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                {
                    return;
                }

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // 广播游戏状态更新到房间内所有玩家
                // Simplified: Just notify the client
                Client.OnPlayerGameStateUpdate(roomId, playerId, gameState, timestamp);
                
                _logger.LogDebug("更新玩家游戏状态: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新玩家游戏状态失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
        }

        /// <summary>
        /// 设置玩家观察者模式
        /// </summary>
        public async ValueTask SetPlayerObserverModeAsync(string roomId, string playerId, bool isObserver)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                {
                    return;
                }

                if (isObserver)
                {
                    // 加入观察者连接
                    var observerKey = $"observer_{roomId}";
                    _observerConnections.AddOrUpdate(observerKey, new HashSet<string> { playerId },
                        (key, existingSet) =>
                        {
                            lock (existingSet)
                            {
                                existingSet.Add(playerId);
                                return existingSet;
                            }
                        });
                }
                else
                {
                    // 从观察者连接中移除
                    RemoveObserverFromRoom(playerId, roomId);
                }

                _logger.LogInformation("设置玩家观察者模式: RoomId={RoomId}, PlayerId={PlayerId}, IsObserver={IsObserver}",
                    roomId, playerId, isObserver);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置玩家观察者模式失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
        }

        #endregion

        #region 游戏流程同步API实现

        /// <summary>
        /// 开始游戏倒计时
        /// </summary>
        public async ValueTask StartGameCountdownAsync(string roomId, string ownerId, int countdownSeconds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(ownerId))
                {
                    Client.OnRoomError(roomId, "INVALID_PARAMS", "房间ID和房主ID不能为空");
                    return;
                }

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                
                // 检查权限
                var hasPermission = await roomGrain.HasPermissionAsync(ownerId, RoomOperation.StartGame);
                if (!hasPermission)
                {
                    Client.OnRoomError(roomId, "NO_PERMISSION", "没有权限开始游戏");
                    return;
                }

                var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                _logger.LogInformation("开始游戏倒计时: RoomId={RoomId}, CountdownSeconds={CountdownSeconds}", roomId, countdownSeconds);

                // 广播倒计时开始到房间内所有玩家
                var roomKey = $"room_{roomId}";
                if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
                {
                    roomGroup.All.OnGameCountdownStart(roomId, countdownSeconds, startTime);
                }
                else
                {
                    Client.OnGameCountdownStart(roomId, countdownSeconds, startTime);
                }

                // TODO: 实现倒计时逻辑，可以使用定时器定期广播倒计时更新
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "开始游戏倒计时失败: RoomId={RoomId}", roomId);
                Client.OnRoomError(roomId, "START_COUNTDOWN_FAILED", "开始倒计时失败");
            }
        }

        /// <summary>
        /// 取消游戏倒计时
        /// </summary>
        public async ValueTask CancelGameCountdownAsync(string roomId, string ownerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(ownerId))
                {
                    return;
                }

                _logger.LogInformation("取消游戏倒计时: RoomId={RoomId}", roomId);

                // 广播倒计时取消到房间内所有玩家
                var roomKey = $"room_{roomId}";
                if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
                {
                    roomGroup.All.OnGameCountdownCancel(roomId, "房主取消");
                }
                else
                {
                    Client.OnGameCountdownCancel(roomId, "房主取消");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消游戏倒计时失败: RoomId={RoomId}", roomId);
            }
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        public async ValueTask StartGameAsync(string roomId, string ownerId, string gameConfig)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(ownerId))
                {
                    Client.OnRoomError(roomId, "INVALID_PARAMS", "房间ID和房主ID不能为空");
                    return;
                }

                var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                _logger.LogInformation("开始游戏: RoomId={RoomId}", roomId);

                // 广播游戏开始到房间内所有玩家
                var roomKey = $"room_{roomId}";
                if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
                {
                    roomGroup.All.OnGameStart(roomId, gameConfig, startTime);
                }
                else
                {
                    Client.OnGameStart(roomId, gameConfig, startTime);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "开始游戏失败: RoomId={RoomId}", roomId);
                Client.OnRoomError(roomId, "START_GAME_FAILED", "开始游戏失败");
            }
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        public async ValueTask EndGameAsync(string roomId, string requesterId, string gameResult)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId))
                {
                    return;
                }

                var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                _logger.LogInformation("结束游戏: RoomId={RoomId}", roomId);

                // 广播游戏结束
                // Simplified: Just notify the client
                Client.OnGameEnd(roomId, gameResult, endTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "结束游戏失败: RoomId={RoomId}", roomId);
            }
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public async ValueTask PauseGameAsync(string roomId, string requesterId, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId))
                {
                    return;
                }

                var pauseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                _logger.LogInformation("暂停游戏: RoomId={RoomId}, Reason={Reason}", roomId, reason);

                // 广播游戏暂停
                // Simplified: Just notify the client
                Client.OnGamePause(roomId, reason, pauseTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "暂停游戏失败: RoomId={RoomId}", roomId);
            }
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public async ValueTask ResumeGameAsync(string roomId, string requesterId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId))
                {
                    return;
                }

                var resumeTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                _logger.LogInformation("恢复游戏: RoomId={RoomId}", roomId);

                // 广播游戏恢复
                // Simplified: Just notify the client
                Client.OnGameResume(roomId, resumeTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "恢复游戏失败: RoomId={RoomId}", roomId);
            }
        }

        #endregion

        #region 其他API简化实现

        public async ValueTask SubmitPlayerActionAsync(string roomId, string playerId, string actionType, string actionData, long timestamp)
        {
            try
            {
                var actionId = Guid.NewGuid().ToString();
                // Simplified: Just notify the client
                Client.OnPlayerActionSubmit(roomId, playerId, actionId, actionType, actionData, timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "提交玩家行动失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
        }

        public async ValueTask UndoPlayerActionAsync(string roomId, string playerId, string actionId)
        {
            try
            {
                // Simplified: Just notify the client
                Client.OnPlayerActionUndo(roomId, playerId, actionId, "玩家撤销");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "撤销玩家行动失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
        }

        public async ValueTask ConfirmTurnEndAsync(string roomId, string playerId, string turnId)
        {
            try
            {
                // Simplified: Just notify the client
                Client.OnTurnEndConfirm(roomId, playerId, turnId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "确认回合结束失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
        }

        public async ValueTask SyncGameObjectStateAsync(string roomId, string objectId, string objectType, string objectState, string updaterId)
        {
            try
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                // Simplified: Just notify the client
                Client.OnGameObjectStateSync(roomId, objectId, objectType, objectState, updaterId, timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步游戏对象状态失败: RoomId={RoomId}, ObjectId={ObjectId}", roomId, objectId);
            }
        }

        public async ValueTask BroadcastGameEventAsync(string roomId, string eventType, string eventData, string sourcePlayerId)
        {
            try
            {
                var eventId = Guid.NewGuid().ToString();
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                // Simplified: Just notify the client
                Client.OnGameEventBroadcast(roomId, eventId, eventType, eventData, sourcePlayerId, timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播游戏事件失败: RoomId={RoomId}, EventType={EventType}", roomId, eventType);
            }
        }

        public async ValueTask RequestSyncCheckAsync(string roomId, string playerId, string checksum)
        {
            try
            {
                // TODO: 实现同步检查逻辑
                var serverChecksum = "server_checksum_placeholder";
                var isSynced = checksum == serverChecksum;
                
                Client.OnSyncCheckResult(roomId, playerId, isSynced, serverChecksum, 
                    isSynced ? null : "客户端与服务器状态不同步");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步检查失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
        }

        public async ValueTask StartObservingRoomAsync(string roomId, string observerId, string accessToken)
        {
            // TODO: 实现观察者功能
        }

        public async ValueTask StopObservingRoomAsync(string roomId, string observerId)
        {
            // TODO: 实现停止观察功能
        }

        public async ValueTask SwitchObserverViewAsync(string roomId, string observerId, string? targetPlayerId)
        {
            // TODO: 实现观察视角切换
        }

        public async ValueTask KickPlayerFromRoomAsync(string roomId, string ownerId, string targetPlayerId, string reason)
        {
            try
            {
                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(targetPlayerId);
                var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);
                var playerName = playerInfo?.DisplayName ?? targetPlayerId;
                
                // Simplified: Just notify the client
                Client.OnPlayerKicked(roomId, targetPlayerId, playerName, ownerId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "踢出玩家失败: RoomId={RoomId}, TargetPlayerId={TargetPlayerId}", roomId, targetPlayerId);
            }
        }

        public async ValueTask TransferRoomOwnershipAsync(string roomId, string currentOwnerId, string newOwnerId)
        {
            try
            {
                var newOwnerGrain = _grainFactory.GetGrain<IPlayerGrain>(newOwnerId);
                var newOwnerInfo = await newOwnerGrain.GetPlayerInfoAsync(false, false);
                var newOwnerName = newOwnerInfo?.DisplayName ?? newOwnerId;
                
                // Simplified: Just notify the client
                Client.OnOwnershipTransfer(roomId, currentOwnerId, newOwnerId, newOwnerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "转移房主权限失败: RoomId={RoomId}, NewOwnerId={NewOwnerId}", roomId, newOwnerId);
            }
        }

        public async ValueTask SetPlayerPermissionsAsync(string roomId, string ownerId, string playerId, string permissions)
        {
            try
            {
                // Simplified: Just notify the client
                Client.OnPlayerPermissionsUpdate(roomId, playerId, permissions, ownerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置玩家权限失败: RoomId={RoomId}, PlayerId={PlayerId}", roomId, playerId);
            }
        }

        #endregion

        #region 辅助方法

        // Broadcast methods removed - using simplified Client notifications instead
        // TODO: Implement proper MagicOnion Group broadcasting

        /// <summary>
        /// 从所有房间移除玩家并发送广播通知
        /// </summary>
        private async ValueTask RemovePlayerFromAllRoomsWithBroadcast(string playerId)
        {
            // 获取玩家信息用于广播
            try
            {
                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);
                var playerName = playerInfo?.DisplayName ?? playerId;

                // 遍历所有房间群组并发送离开通知
                foreach (var roomGroupPair in _roomGroups.ToList())
                {
                    var roomKey = roomGroupPair.Key;
                    var roomGroup = roomGroupPair.Value;
                    var roomId = roomKey.Replace("room_", "");

                    // 检查玩家是否在这个房间中
                    if (_roomConnections.TryGetValue(roomKey, out var playerSet) && playerSet.Contains(playerId))
                    {
                        // 向房间内其他玩家广播玩家离开事件
                        roomGroup.All.OnPlayerLeftRoom(roomId, playerId, playerName, "连接断开");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取玩家信息失败，跳过广播通知: PlayerId={PlayerId}", playerId);
            }

            // 清理连接记录
            RemovePlayerFromAllRooms(playerId);

            // 清理群组引用（如果房间为空）
            var emptyRooms = _roomConnections.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
            foreach (var emptyRoomKey in emptyRooms)
            {
                if (_roomGroups.TryRemove(emptyRoomKey, out var removedGroup))
                {
                    _logger.LogDebug("清理空房间群组: {RoomKey}", emptyRoomKey);
                }
            }
        }

        /// <summary>
        /// 从所有房间移除玩家
        /// </summary>
        private void RemovePlayerFromAllRooms(string playerId)
        {
            // 从房间连接移除
            foreach (var roomConnection in _roomConnections.ToList())
            {
                if (roomConnection.Value != null)
                {
                    lock (roomConnection.Value)
                    {
                        roomConnection.Value.Remove(playerId);
                        if (roomConnection.Value.Count == 0)
                        {
                            _roomConnections.TryRemove(roomConnection.Key, out _);
                        }
                    }
                }
            }

            // 从观察者连接移除
            foreach (var observerConnection in _observerConnections.ToList())
            {
                if (observerConnection.Value != null)
                {
                    lock (observerConnection.Value)
                    {
                        observerConnection.Value.Remove(playerId);
                        if (observerConnection.Value.Count == 0)
                        {
                            _observerConnections.TryRemove(observerConnection.Key, out _);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 从特定房间移除玩家
        /// </summary>
        private void RemovePlayerFromRoom(string playerId, string roomId)
        {
            var roomKey = $"room_{roomId}";
            if (_roomConnections.TryGetValue(roomKey, out var playerSet))
            {
                lock (playerSet)
                {
                    playerSet.Remove(playerId);
                    if (playerSet.Count == 0)
                    {
                        _roomConnections.TryRemove(roomKey, out _);
                    }
                }
            }
        }

        /// <summary>
        /// 从观察者连接中移除玩家
        /// </summary>
        private void RemoveObserverFromRoom(string observerId, string roomId)
        {
            var observerKey = $"observer_{roomId}";
            if (_observerConnections.TryGetValue(observerKey, out var observerSet))
            {
                lock (observerSet)
                {
                    observerSet.Remove(observerId);
                    if (observerSet.Count == 0)
                    {
                        _observerConnections.TryRemove(observerKey, out _);
                    }
                }
            }
        }

        #endregion
    }
}