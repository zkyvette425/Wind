using Microsoft.Extensions.Logging;
using Orleans;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

namespace Wind.Grains
{
    /// <summary>
    /// 玩家Grain实现
    /// 负责管理单个玩家的状态和行为
    /// 临时使用内存状态，后续添加持久化
    /// </summary>
    public class PlayerGrain : Grain, IPlayerGrain
    {
        private readonly ILogger<PlayerGrain> _logger;
        private PlayerState? _playerState;

        public PlayerGrain(ILogger<PlayerGrain> logger)
        {
            _logger = logger;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var playerId = this.GetPrimaryKeyString();
            _logger.LogInformation("PlayerGrain激活: {PlayerId}", playerId);

            // 初始化玩家状态
            _playerState = new PlayerState
            {
                PlayerId = playerId,
                DisplayName = $"Player_{playerId[..Math.Min(8, playerId.Length)]}", // 默认显示名
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                OnlineStatus = PlayerOnlineStatus.Offline
            };

            await base.OnActivateAsync(cancellationToken);
        }

        public async Task<PlayerLoginResponse> LoginAsync(PlayerLoginRequest request)
        {
            try
            {
                EnsurePlayerStateInitialized();
                _logger.LogInformation("玩家登录请求: {PlayerId}", request.PlayerId);

                // 更新会话信息
                var sessionId = Guid.NewGuid().ToString();
                _playerState!.Session = new PlayerSession
                {
                    SessionId = sessionId,
                    SessionStartTime = DateTime.UtcNow,
                    ClientVersion = request.ClientVersion,
                    Platform = request.Platform,
                    DeviceId = request.DeviceId
                };

                // 更新显示名称（如果提供）
                if (!string.IsNullOrEmpty(request.DisplayName))
                {
                    _playerState.DisplayName = request.DisplayName;
                }

                // 设置在线状态
                _playerState.OnlineStatus = PlayerOnlineStatus.Online;
                _playerState.LastLoginAt = DateTime.UtcNow;
                _playerState.LastActiveAt = DateTime.UtcNow;
                _playerState.Version++;

                var playerInfo = MapToPlayerInfo(_playerState);

                _logger.LogInformation("玩家登录成功: {PlayerId}, SessionId: {SessionId}", 
                    request.PlayerId, sessionId);

                return new PlayerLoginResponse
                {
                    Success = true,
                    Message = "登录成功",
                    SessionId = sessionId,
                    AuthToken = GenerateAuthToken(request.PlayerId, sessionId),
                    PlayerInfo = playerInfo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家登录失败: {PlayerId}", request.PlayerId);
                return new PlayerLoginResponse
                {
                    Success = false,
                    Message = $"登录失败: {ex.Message}"
                };
            }
        }

        public async Task<PlayerLogoutResponse> LogoutAsync(PlayerLogoutRequest request)
        {
            try
            {
                EnsurePlayerStateInitialized();
                _logger.LogInformation("玩家登出: {PlayerId}, 原因: {Reason}", 
                    _playerState!.PlayerId, request.Reason);

                _playerState.OnlineStatus = PlayerOnlineStatus.Offline;
                _playerState.Session.SessionId = string.Empty;
                _playerState.Version++;

                return new PlayerLogoutResponse
                {
                    Success = true,
                    Message = "登出成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家登出失败: {PlayerId}", _playerState?.PlayerId);
                return new PlayerLogoutResponse
                {
                    Success = false,
                    Message = $"登出失败: {ex.Message}"
                };
            }
        }

        public async Task<PlayerInfo?> GetPlayerInfoAsync(bool includeStats = true, bool includeSettings = false)
        {
            await Task.CompletedTask;
            EnsurePlayerStateInitialized();
            _logger.LogDebug("获取玩家信息: {PlayerId}", _playerState!.PlayerId);
            
            var playerInfo = MapToPlayerInfo(_playerState);
            
            if (!includeStats)
            {
                playerInfo.Stats = new PlayerStats();
            }
            
            return playerInfo;
        }

        public async Task<PlayerUpdateResponse> UpdatePlayerAsync(PlayerUpdateRequest request)
        {
            try
            {
                EnsurePlayerStateInitialized();

                // 乐观锁检查
                if (request.Version != _playerState!.Version)
                {
                    return new PlayerUpdateResponse
                    {
                        Success = false,
                        Message = "状态版本不匹配，请刷新后重试",
                        NewVersion = _playerState.Version
                    };
                }

                var updated = false;

                // 更新显示名称
                if (!string.IsNullOrEmpty(request.DisplayName) && 
                    request.DisplayName != _playerState.DisplayName)
                {
                    _playerState.DisplayName = request.DisplayName;
                    updated = true;
                }

                // 更新位置
                if (request.Position != null)
                {
                    _playerState.Position = request.Position;
                    updated = true;
                }

                // 更新在线状态
                if (request.OnlineStatus.HasValue)
                {
                    _playerState.OnlineStatus = request.OnlineStatus.Value;
                    updated = true;
                }

                // 更新设置
                if (request.Settings != null)
                {
                    _playerState.Settings = request.Settings;
                    updated = true;
                }

                if (updated)
                {
                    _playerState.Version++;
                    _playerState.LastActiveAt = DateTime.UtcNow;

                    _logger.LogInformation("玩家信息更新成功: {PlayerId}, 新版本: {Version}", 
                        _playerState.PlayerId, _playerState.Version);
                }

                return new PlayerUpdateResponse
                {
                    Success = true,
                    Message = updated ? "更新成功" : "无需更新",
                    NewVersion = _playerState.Version,
                    UpdatedPlayerInfo = MapToPlayerInfo(_playerState)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家信息更新失败: {PlayerId}", _playerState?.PlayerId);
                return new PlayerUpdateResponse
                {
                    Success = false,
                    Message = $"更新失败: {ex.Message}",
                    NewVersion = _playerState?.Version ?? 0
                };
            }
        }

        public async Task<bool> UpdatePositionAsync(PlayerPosition position)
        {
            try
            {
                EnsurePlayerStateInitialized();
                _playerState!.Position = position;
                _playerState.Position.UpdatedAt = DateTime.UtcNow;
                _playerState.LastActiveAt = DateTime.UtcNow;

                _logger.LogDebug("玩家位置更新: {PlayerId}, 位置: ({X}, {Y}, {Z})", 
                    _playerState.PlayerId, position.X, position.Y, position.Z);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家位置更新失败: {PlayerId}", _playerState?.PlayerId);
                return false;
            }
        }

        public async Task<bool> SetOnlineStatusAsync(PlayerOnlineStatus status)
        {
            try
            {
                EnsurePlayerStateInitialized();
                var oldStatus = _playerState!.OnlineStatus;
                _playerState.OnlineStatus = status;

                _logger.LogInformation("玩家状态变更: {PlayerId}, {OldStatus} -> {NewStatus}", 
                    _playerState.PlayerId, oldStatus, status);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家状态设置失败: {PlayerId}", _playerState?.PlayerId);
                return false;
            }
        }

        public async Task<bool> JoinRoomAsync(string roomId)
        {
            try
            {
                EnsurePlayerStateInitialized();
                if (_playerState!.CurrentRoomId == roomId)
                {
                    return true; // 已经在该房间中
                }

                var oldRoomId = _playerState.CurrentRoomId;
                _playerState.CurrentRoomId = roomId;
                _playerState.OnlineStatus = PlayerOnlineStatus.InGame;

                _logger.LogInformation("玩家加入房间: {PlayerId}, 从房间 {OldRoomId} 到房间 {NewRoomId}", 
                    _playerState.PlayerId, oldRoomId, roomId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家加入房间失败: {PlayerId}, 房间: {RoomId}", 
                    _playerState?.PlayerId, roomId);
                return false;
            }
        }

        public async Task<bool> LeaveRoomAsync()
        {
            try
            {
                EnsurePlayerStateInitialized();
                var oldRoomId = _playerState!.CurrentRoomId;
                _playerState.CurrentRoomId = null;
                _playerState.OnlineStatus = PlayerOnlineStatus.Online;

                _logger.LogInformation("玩家离开房间: {PlayerId}, 房间: {RoomId}", 
                    _playerState.PlayerId, oldRoomId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家离开房间失败: {PlayerId}", _playerState?.PlayerId);
                return false;
            }
        }

        public async Task<string?> GetCurrentRoomAsync()
        {
            await Task.CompletedTask;
            EnsurePlayerStateInitialized();
            return _playerState!.CurrentRoomId;
        }

        public async Task<bool> UpdateStatsAsync(PlayerStats stats)
        {
            try
            {
                EnsurePlayerStateInitialized();
                _playerState!.Stats = stats;
                _playerState.LastActiveAt = DateTime.UtcNow;

                _logger.LogInformation("玩家统计更新: {PlayerId}, 游戏场数: {GamesPlayed}", 
                    _playerState.PlayerId, stats.GamesPlayed);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家统计更新失败: {PlayerId}", _playerState?.PlayerId);
                return false;
            }
        }

        public async Task<bool> UpdateSettingsAsync(PlayerSettings settings)
        {
            try
            {
                EnsurePlayerStateInitialized();
                _playerState!.Settings = settings;
                _playerState.LastActiveAt = DateTime.UtcNow;

                _logger.LogInformation("玩家设置更新: {PlayerId}", _playerState.PlayerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家设置更新失败: {PlayerId}", _playerState?.PlayerId);
                return false;
            }
        }

        public async Task<bool> IsOnlineAsync()
        {
            await Task.CompletedTask;
            EnsurePlayerStateInitialized();
            return _playerState!.OnlineStatus != PlayerOnlineStatus.Offline;
        }

        public async Task<DateTime> GetLastActiveTimeAsync()
        {
            await Task.CompletedTask;
            EnsurePlayerStateInitialized();
            return _playerState!.LastActiveAt;
        }

        public async Task<bool> HeartbeatAsync()
        {
            try
            {
                EnsurePlayerStateInitialized();
                _playerState!.LastActiveAt = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "心跳更新失败: {PlayerId}", _playerState?.PlayerId);
                return false;
            }
        }

        public async Task<bool> ValidateSessionAsync(string sessionId)
        {
            await Task.CompletedTask;
            
            // 检查输入参数有效性
            if (string.IsNullOrEmpty(sessionId))
            {
                return false;
            }
            
            EnsurePlayerStateInitialized();
            return _playerState!.Session.SessionId == sessionId && 
                   _playerState.Session.SessionExpireTime > DateTime.UtcNow;
        }

        public async Task<PlayerState?> GetFullStateAsync()
        {
            await Task.CompletedTask;
            EnsurePlayerStateInitialized();
            return _playerState;
        }

        public async Task<bool> SaveStateAsync()
        {
            try
            {
                EnsurePlayerStateInitialized();
                // 目前使用内存存储，无需实际保存
                _logger.LogDebug("玩家状态强制保存: {PlayerId}", _playerState!.PlayerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家状态保存失败: {PlayerId}", _playerState?.PlayerId);
                return false;
            }
        }

        /// <summary>
        /// 确保玩家状态已初始化
        /// </summary>
        private void EnsurePlayerStateInitialized()
        {
            if (_playerState == null)
            {
                var playerId = this.GetPrimaryKeyString();
                _playerState = new PlayerState
                {
                    PlayerId = playerId,
                    DisplayName = $"Player_{playerId[..Math.Min(8, playerId.Length)]}",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow,
                    OnlineStatus = PlayerOnlineStatus.Offline
                };
                _logger.LogWarning("延迟初始化玩家状态: {PlayerId}", playerId);
            }
        }

        /// <summary>
        /// 将PlayerState转换为PlayerInfo DTO
        /// </summary>
        private static PlayerInfo MapToPlayerInfo(PlayerState state)
        {
            return new PlayerInfo
            {
                PlayerId = state.PlayerId,
                DisplayName = state.DisplayName,
                Level = state.Level,
                Experience = state.Experience,
                OnlineStatus = state.OnlineStatus,
                LastLoginAt = state.LastLoginAt,
                Stats = state.Stats,
                Position = state.Position
            };
        }

        /// <summary>
        /// 生成认证令牌（简化版本）
        /// </summary>
        private static string GenerateAuthToken(string playerId, string sessionId)
        {
            // 在实际应用中，这里应该生成JWT令牌
            var tokenData = $"{playerId}:{sessionId}:{DateTime.UtcNow:yyyyMMddHHmmss}";
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(tokenData));
        }
    }
}