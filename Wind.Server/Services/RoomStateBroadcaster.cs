using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wind.Shared.Services;
using Wind.Shared.Models;
using MagicOnion.Server.Hubs;
using System.Text.Json;

namespace Wind.Server.Services
{
    /// <summary>
    /// 房间状态广播管理器
    /// 负责将RoomGrain的状态变更实时广播给房间内所有PlayerHub连接的客户端
    /// </summary>
    public class RoomStateBroadcaster
    {
        private readonly ILogger<RoomStateBroadcaster> _logger;
        
        public RoomStateBroadcaster(ILogger<RoomStateBroadcaster> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        #region 房间整体状态广播
        
        /// <summary>
        /// 广播完整房间状态更新
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="roomState">完整房间状态</param>
        public async ValueTask BroadcastRoomStateUpdate(IGroup<IPlayerHubReceiver>? room, RoomState roomState)
        {
            if (room == null)
            {
                _logger.LogWarning("房间群组为空，无法广播房间状态");
                return;
            }
            
            try
            {
                var roomStateJson = JsonSerializer.Serialize(roomState);
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                // 广播给房间内所有玩家
                room.All.OnGameStateUpdate(roomState.RoomId, roomStateJson, timestamp);
                
                _logger.LogDebug("广播房间状态更新: RoomId={RoomId}, PlayerCount={PlayerCount}, Status={Status}",
                    roomState.RoomId, roomState.CurrentPlayerCount, roomState.Status);
                    
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播房间状态更新时发生错误: RoomId={RoomId}", roomState.RoomId);
                throw;
            }
        }
        
        /// <summary>
        /// 广播房间设置变更
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="roomId">房间ID</param>
        /// <param name="newSettings">新设置</param>
        /// <param name="operatorId">操作者ID</param>
        public async ValueTask BroadcastRoomSettingsChanged(IGroup<IPlayerHubReceiver>? room, 
            string roomId, RoomSettings newSettings, string operatorId)
        {
            if (room == null) return;
            
            try
            {
                var settingsJson = JsonSerializer.Serialize(newSettings);
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                // 发送系统通知
                room.All.OnSystemNotification("ROOM_SETTINGS_CHANGED", "房间设置变更", 
                    $"房间设置已由 {operatorId} 更新", timestamp);
                
                // 更新房间状态
                room.All.OnGameStateUpdate(roomId, settingsJson, timestamp);
                
                _logger.LogInformation("广播房间设置变更: RoomId={RoomId}, Operator={Operator}", roomId, operatorId);
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播房间设置变更时发生错误: RoomId={RoomId}", roomId);
                throw;
            }
        }
        
        #endregion
        
        #region 玩家状态广播
        
        /// <summary>
        /// 广播玩家加入房间
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="roomId">房间ID</param>
        /// <param name="newPlayer">新加入的玩家</param>
        public async ValueTask BroadcastPlayerJoined(IGroup<IPlayerHubReceiver>? room, 
            string roomId, RoomPlayer newPlayer)
        {
            if (room == null) return;
            
            try
            {
                // 广播玩家加入事件
                room.All.OnPlayerJoinedRoom(roomId, newPlayer.PlayerId, newPlayer.DisplayName);
                
                // 发送系统通知
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                room.All.OnSystemNotification("PLAYER_JOINED", "玩家加入", 
                    $"{newPlayer.DisplayName} 加入了房间", timestamp);
                
                _logger.LogInformation("广播玩家加入: RoomId={RoomId}, PlayerId={PlayerId}, PlayerName={PlayerName}",
                    roomId, newPlayer.PlayerId, newPlayer.DisplayName);
                    
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播玩家加入时发生错误: RoomId={RoomId}, PlayerId={PlayerId}", 
                    roomId, newPlayer.PlayerId);
                throw;
            }
        }
        
        /// <summary>
        /// 广播玩家离开房间
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="roomId">房间ID</param>
        /// <param name="leftPlayer">离开的玩家</param>
        /// <param name="reason">离开原因</param>
        public async ValueTask BroadcastPlayerLeft(IGroup<IPlayerHubReceiver>? room, 
            string roomId, RoomPlayer leftPlayer, string reason = "USER_LEFT")
        {
            if (room == null) return;
            
            try
            {
                // 广播玩家离开事件
                room.All.OnPlayerLeftRoom(roomId, leftPlayer.PlayerId, leftPlayer.DisplayName);
                
                // 发送系统通知
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var reasonText = reason switch
                {
                    "USER_LEFT" => "主动离开",
                    "KICKED" => "被踢出房间", 
                    "DISCONNECTED" => "连接断开",
                    "TIMEOUT" => "连接超时",
                    _ => "离开房间"
                };
                
                room.All.OnSystemNotification("PLAYER_LEFT", "玩家离开", 
                    $"{leftPlayer.DisplayName} {reasonText}", timestamp);
                
                _logger.LogInformation("广播玩家离开: RoomId={RoomId}, PlayerId={PlayerId}, Reason={Reason}",
                    roomId, leftPlayer.PlayerId, reason);
                    
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播玩家离开时发生错误: RoomId={RoomId}, PlayerId={PlayerId}", 
                    roomId, leftPlayer.PlayerId);
                throw;
            }
        }
        
        /// <summary>
        /// 广播玩家准备状态变更
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="roomPlayer">房间内玩家信息</param>
        public async ValueTask BroadcastPlayerReadyStatusChanged(IGroup<IPlayerHubReceiver>? room, 
            RoomPlayer roomPlayer)
        {
            if (room == null) return;
            
            try
            {
                var isReady = roomPlayer.ReadyStatus == PlayerReadyStatus.Ready;
                
                // 广播准备状态变更
                room.All.OnPlayerReadyStatusChanged(roomPlayer.PlayerId, roomPlayer.DisplayName, isReady);
                
                // 发送系统通知
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var statusText = roomPlayer.ReadyStatus switch
                {
                    PlayerReadyStatus.Ready => "已准备",
                    PlayerReadyStatus.NotReady => "取消准备",
                    PlayerReadyStatus.Loading => "加载中",
                    _ => "状态未知"
                };
                
                room.All.OnSystemNotification("PLAYER_READY_CHANGED", "准备状态变更", 
                    $"{roomPlayer.DisplayName} {statusText}", timestamp);
                
                _logger.LogDebug("广播玩家准备状态: PlayerId={PlayerId}, Status={Status}",
                    roomPlayer.PlayerId, roomPlayer.ReadyStatus);
                    
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播玩家准备状态时发生错误: PlayerId={PlayerId}", roomPlayer.PlayerId);
                throw;
            }
        }
        
        /// <summary>
        /// 广播玩家位置更新
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="position">新位置</param>
        /// <param name="excludePlayer">排除的连接ID (通常是发送者自己)</param>
        public async ValueTask BroadcastPlayerPositionUpdate(IGroup<IPlayerHubReceiver>? room, 
            string playerId, PlayerPosition position, IEnumerable<Guid>? excludePlayer = null)
        {
            if (room == null) return;
            
            try
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var receiver = excludePlayer != null ? room.Except(excludePlayer) : room.All;
                
                // 广播位置更新 (排除发送者自己)
                receiver.OnPlayerPositionUpdated(playerId, position.X, position.Y, position.Z, timestamp);
                
                _logger.LogTrace("广播玩家位置更新: PlayerId={PlayerId}, Position=({X},{Y},{Z})",
                    playerId, position.X, position.Y, position.Z);
                    
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播玩家位置更新时发生错误: PlayerId={PlayerId}", playerId);
                throw;
            }
        }
        
        #endregion
        
        #region 游戏状态广播
        
        /// <summary>
        /// 广播游戏开始
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="roomState">房间状态</param>
        public async ValueTask BroadcastGameStarted(IGroup<IPlayerHubReceiver>? room, RoomState roomState)
        {
            if (room == null) return;
            
            try
            {
                var gameSettings = JsonSerializer.Serialize(roomState.Settings);
                var startTimestamp = roomState.GameStartTime?.ToUnixTimeSeconds() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                // 广播游戏开始
                room.All.OnGameStart(roomState.RoomId, gameSettings, startTimestamp);
                
                // 发送系统通知
                room.All.OnSystemNotification("GAME_STARTED", "游戏开始", 
                    $"游戏模式: {roomState.Settings.GameMode}", startTimestamp);
                
                _logger.LogInformation("广播游戏开始: RoomId={RoomId}, GameMode={GameMode}, PlayerCount={PlayerCount}",
                    roomState.RoomId, roomState.Settings.GameMode, roomState.CurrentPlayerCount);
                    
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播游戏开始时发生错误: RoomId={RoomId}", roomState.RoomId);
                throw;
            }
        }
        
        /// <summary>
        /// 广播游戏结束
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="roomState">房间状态</param>
        /// <param name="gameResult">游戏结果</param>
        public async ValueTask BroadcastGameEnded(IGroup<IPlayerHubReceiver>? room, 
            RoomState roomState, Dictionary<string, object> gameResult)
        {
            if (room == null) return;
            
            try
            {
                var gameResultJson = JsonSerializer.Serialize(gameResult);
                var endTimestamp = roomState.GameEndTime?.ToUnixTimeSeconds() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                // 广播游戏结束
                room.All.OnGameEnd(roomState.RoomId, gameResultJson, endTimestamp);
                
                // 发送获胜者通知
                if (gameResult.TryGetValue("winner", out var winner))
                {
                    room.All.OnSystemNotification("GAME_ENDED", "游戏结束", 
                        $"获胜者: {winner}", endTimestamp);
                }
                else
                {
                    room.All.OnSystemNotification("GAME_ENDED", "游戏结束", 
                        "游戏已结束", endTimestamp);
                }
                
                _logger.LogInformation("广播游戏结束: RoomId={RoomId}, Winner={Winner}",
                    roomState.RoomId, gameResult.GetValueOrDefault("winner", "无"));
                    
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播游戏结束时发生错误: RoomId={RoomId}", roomState.RoomId);
                throw;
            }
        }
        
        /// <summary>
        /// 广播实时分数更新
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerScores">玩家分数字典</param>
        public async ValueTask BroadcastScoreUpdate(IGroup<IPlayerHubReceiver>? room, 
            string roomId, Dictionary<string, int> playerScores)
        {
            if (room == null) return;
            
            try
            {
                var scoresJson = JsonSerializer.Serialize(playerScores);
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                // 更新游戏状态
                room.All.OnGameStateUpdate(roomId, scoresJson, timestamp);
                
                _logger.LogDebug("广播分数更新: RoomId={RoomId}, ScoreCount={Count}", roomId, playerScores.Count);
                
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播分数更新时发生错误: RoomId={RoomId}", roomId);
                throw;
            }
        }
        
        #endregion
        
        #region 房间事件广播
        
        /// <summary>
        /// 广播房间事件
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="roomEvent">房间事件</param>
        public async ValueTask BroadcastRoomEvent(IGroup<IPlayerHubReceiver>? room, RoomEvent roomEvent)
        {
            if (room == null) return;
            
            try
            {
                var eventType = roomEvent.EventType.ToString().ToUpper();
                var timestamp = roomEvent.Timestamp.ToUnixTimeSeconds();
                
                // 根据事件类型发送相应通知
                switch (roomEvent.EventType)
                {
                    case RoomEventType.PlayerJoined:
                    case RoomEventType.PlayerLeft:
                    case RoomEventType.PlayerReady:
                    case RoomEventType.PlayerNotReady:
                        // 这些事件已通过专门的方法处理
                        break;
                        
                    case RoomEventType.GameStarted:
                    case RoomEventType.GameEnded:
                        room.All.OnSystemNotification(eventType, "游戏状态变更", roomEvent.Description, timestamp);
                        break;
                        
                    case RoomEventType.RoomSettingsChanged:
                        room.All.OnSystemNotification(eventType, "房间设置", roomEvent.Description, timestamp);
                        break;
                        
                    case RoomEventType.PlayerKicked:
                        room.All.OnSystemNotification(eventType, "玩家管理", roomEvent.Description, timestamp);
                        break;
                        
                    case RoomEventType.RoomClosed:
                        room.All.OnSystemNotification(eventType, "房间关闭", roomEvent.Description, timestamp);
                        break;
                        
                    default:
                        room.All.OnSystemNotification(eventType, "房间事件", roomEvent.Description, timestamp);
                        break;
                }
                
                _logger.LogDebug("广播房间事件: EventType={EventType}, PlayerId={PlayerId}, Description={Description}",
                    roomEvent.EventType, roomEvent.PlayerId, roomEvent.Description);
                    
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播房间事件时发生错误: EventId={EventId}", roomEvent.EventId);
                throw;
            }
        }
        
        #endregion
        
        #region 批量广播优化
        
        /// <summary>
        /// 批量广播多个房间事件 (性能优化)
        /// </summary>
        /// <param name="room">房间群组</param>
        /// <param name="events">事件列表</param>
        public async ValueTask BroadcastRoomEventsBatch(IGroup<IPlayerHubReceiver>? room, 
            IEnumerable<RoomEvent> events)
        {
            if (room == null) return;
            
            try
            {
                var eventList = events.ToList();
                if (eventList.Count == 0) return;
                
                _logger.LogDebug("批量广播房间事件: EventCount={Count}", eventList.Count);
                
                // 批量处理事件以提高性能
                var tasks = eventList.Select(roomEvent => BroadcastRoomEvent(room, roomEvent));
                await Task.WhenAll(tasks.Select(t => t.AsTask()));
                
                _logger.LogDebug("批量广播房间事件完成: EventCount={Count}", eventList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量广播房间事件时发生错误");
                throw;
            }
        }
        
        #endregion
    }
}