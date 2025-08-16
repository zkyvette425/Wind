using Microsoft.Extensions.Logging;
using Orleans;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Shared.Services;
using Wind.Shared.Extensions;

namespace Wind.Grains
{
    /// <summary>
    /// 玩家Grain实现
    /// 负责管理单个玩家的状态和行为
    /// 使用分布式锁保护并发操作（持久化将在后续添加）
    /// </summary>
    public class PlayerGrain : Grain, IPlayerGrain
    {
        private readonly ILogger<PlayerGrain> _logger;
        private readonly IDistributedLock _distributedLock;
        private PlayerState? _playerState;

        public PlayerGrain(
            ILogger<PlayerGrain> logger,
            IDistributedLock distributedLock)
        {
            _logger = logger;
            _distributedLock = distributedLock;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var playerId = this.GetPrimaryKeyString();
            _logger.LogInformation("PlayerGrain激活: {PlayerId}", playerId);

            // 初始化玩家状态（内存版本，持久化将在后续添加）
            _playerState = new PlayerState
            {
                PlayerId = playerId,
                DisplayName = $"Player_{playerId[..Math.Min(8, playerId.Length)]}", // 默认显示名
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                OnlineStatus = PlayerOnlineStatus.Offline,
                Version = 1
            };

            _logger.LogInformation("创建新玩家状态: {PlayerId}", playerId);

            await base.OnActivateAsync(cancellationToken);
        }

        public async Task<PlayerLoginResponse> LoginAsync(PlayerLoginRequest request)
        {
            return await _distributedLock.WithPlayerLockAsync(request.PlayerId, async () =>
            {
                try
                {
                    _logger.LogInformation("玩家登录请求: {PlayerId}", request.PlayerId);

                    // 确保状态已初始化
                    if (_playerState == null)
                    {
                        throw new InvalidOperationException("玩家状态未初始化");
                    }

                    // 更新会话信息
                    var sessionId = Guid.NewGuid().ToString();
                    _playerState.Session = new PlayerSession
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

                    // TODO: 保存状态到Redis（持久化将在后续添加）

                    var playerInfo = MapToPlayerInfo(_playerState);

                    _logger.LogInformation("玩家登录成功: {PlayerId}, SessionId: {SessionId}, 版本: {Version}", 
                        request.PlayerId, sessionId, _playerState.Version);

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
            });
        }

        public async Task<PlayerLogoutResponse> LogoutAsync(PlayerLogoutRequest request)
        {
            var playerId = this.GetPrimaryKeyString();
            return await _distributedLock.WithPlayerLockAsync(playerId, async () =>
            {
                try
                {
                    _logger.LogInformation("玩家登出: {PlayerId}, 原因: {Reason}", playerId, request.Reason);

                    // 确保状态已初始化
                    if (_playerState == null)
                    {
                        throw new InvalidOperationException("玩家状态未初始化");
                    }

                    _playerState.OnlineStatus = PlayerOnlineStatus.Offline;
                    _playerState.Session.SessionId = string.Empty;
                    _playerState.Version++;
                    _playerState.LastActiveAt = DateTime.UtcNow;

                    // TODO: 保存状态到Redis（持久化将在后续添加）

                    _logger.LogInformation("玩家登出成功: {PlayerId}, 版本: {Version}", playerId, _playerState.Version);

                    return new PlayerLogoutResponse
                    {
                        Success = true,
                        Message = "登出成功"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "玩家登出失败: {PlayerId}", playerId);
                    return new PlayerLogoutResponse
                    {
                        Success = false,
                        Message = $"登出失败: {ex.Message}"
                    };
                }
            });
        }

        public async Task<PlayerInfo?> GetPlayerInfoAsync(bool includeStats = true, bool includeSettings = false)
        {
            var playerId = this.GetPrimaryKeyString();
            
            // 确保状态已初始化
            if (_playerState == null)
            {
                throw new InvalidOperationException("玩家状态未初始化");
            }

            if (_playerState == null || string.IsNullOrEmpty(_playerState.PlayerId))
            {
                _logger.LogWarning("玩家状态不存在: {PlayerId}", playerId);
                return null;
            }

            _logger.LogDebug("获取玩家信息: {PlayerId}", _playerState.PlayerId);
            
            var playerInfo = MapToPlayerInfo(_playerState);
            
            if (!includeStats)
            {
                playerInfo.Stats = new PlayerStats();
            }
            
            return playerInfo;
        }

        public async Task<PlayerUpdateResponse> UpdatePlayerAsync(PlayerUpdateRequest request)
        {
            var playerId = this.GetPrimaryKeyString();
            return await _distributedLock.WithPlayerLockAsync(playerId, async () =>
            {
                try
                {
                    // 确保状态已初始化
                    if (_playerState == null)
                    {
                        throw new InvalidOperationException("玩家状态未初始化");
                    }

                    if (_playerState == null)
                    {
                        return new PlayerUpdateResponse
                        {
                            Success = false,
                            Message = "玩家状态不存在",
                            NewVersion = 0
                        };
                    }

                    // 乐观锁检查
                    if (request.Version != _playerState.Version)
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

                        // 保存状态到Redis
                        // TODO: 保存状态到Redis（持久化将在后续添加）

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
                    _logger.LogError(ex, "玩家信息更新失败: {PlayerId}", playerId);
                    return new PlayerUpdateResponse
                    {
                        Success = false,
                        Message = $"更新失败: {ex.Message}",
                        NewVersion = _playerState?.Version ?? 0
                    };
                }
            });
        }

        public async Task<bool> UpdatePositionAsync(PlayerPosition position)
        {
            var playerId = this.GetPrimaryKeyString();
            return await _distributedLock.WithPlayerLockAsync(playerId, async () =>
            {
                try
                {
                    // 确保状态已初始化
                    if (_playerState == null)
                    {
                        throw new InvalidOperationException("玩家状态未初始化");
                    }

                    if (_playerState == null)
                    {
                        _logger.LogWarning("玩家状态不存在，无法更新位置: {PlayerId}", playerId);
                        return false;
                    }

                    _playerState.Position = position;
                    _playerState.Position.UpdatedAt = DateTime.UtcNow;
                    _playerState.LastActiveAt = DateTime.UtcNow;

                    // 位置更新不增加版本号（高频操作）
                    // _playerState.Version++;

                    // 批量保存位置更新（减少Redis写入频率）
                    // TODO: 保存状态到Redis（持久化将在后续添加）

                    _logger.LogDebug("玩家位置更新: {PlayerId}, 位置: ({X}, {Y}, {Z})", 
                        _playerState.PlayerId, position.X, position.Y, position.Z);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "玩家位置更新失败: {PlayerId}", playerId);
                    return false;
                }
            }, 
            expiry: TimeSpan.FromSeconds(30), // 位置锁时间较短
            timeout: TimeSpan.FromSeconds(5)   // 位置更新超时时间较短
            );
        }

        public async Task<bool> SetOnlineStatusAsync(PlayerOnlineStatus status)
        {
            var playerId = this.GetPrimaryKeyString();
            return await _distributedLock.WithPlayerLockAsync(playerId, async () =>
            {
                try
                {
                    // 确保状态已初始化
                    if (_playerState == null)
                    {
                        throw new InvalidOperationException("玩家状态未初始化");
                    }

                    if (_playerState == null)
                    {
                        return false;
                    }

                    var oldStatus = _playerState.OnlineStatus;
                    _playerState.OnlineStatus = status;
                    _playerState.LastActiveAt = DateTime.UtcNow;

                    // TODO: 保存状态到Redis（持久化将在后续添加）

                    _logger.LogInformation("玩家状态变更: {PlayerId}, {OldStatus} -> {NewStatus}", 
                        _playerState.PlayerId, oldStatus, status);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "玩家状态设置失败: {PlayerId}", playerId);
                    return false;
                }
            });
        }

        public async Task<bool> JoinRoomAsync(string roomId)
        {
            var playerId = this.GetPrimaryKeyString();
            return await _distributedLock.WithPlayerLockAsync(playerId, async () =>
            {
                try
                {
                    // 确保状态已初始化
                    if (_playerState == null)
                    {
                        throw new InvalidOperationException("玩家状态未初始化");
                    }

                    if (_playerState == null)
                    {
                        return false;
                    }

                    if (_playerState.CurrentRoomId == roomId)
                    {
                        return true; // 已经在该房间中
                    }

                    var oldRoomId = _playerState.CurrentRoomId;
                    _playerState.CurrentRoomId = roomId;
                    _playerState.OnlineStatus = PlayerOnlineStatus.InGame;
                    _playerState.LastActiveAt = DateTime.UtcNow;

                    // TODO: 保存状态到Redis（持久化将在后续添加）

                    _logger.LogInformation("玩家加入房间: {PlayerId}, 从房间 {OldRoomId} 到房间 {NewRoomId}", 
                        _playerState.PlayerId, oldRoomId, roomId);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "玩家加入房间失败: {PlayerId}, 房间: {RoomId}", playerId, roomId);
                    return false;
                }
            });
        }

        public async Task<bool> LeaveRoomAsync()
        {
            var playerId = this.GetPrimaryKeyString();
            return await _distributedLock.WithPlayerLockAsync(playerId, async () =>
            {
                try
                {
                    // 确保状态已初始化
                    if (_playerState == null)
                    {
                        throw new InvalidOperationException("玩家状态未初始化");
                    }

                    if (_playerState == null)
                    {
                        return false;
                    }

                    var oldRoomId = _playerState.CurrentRoomId;
                    _playerState.CurrentRoomId = null;
                    _playerState.OnlineStatus = PlayerOnlineStatus.Online;
                    _playerState.LastActiveAt = DateTime.UtcNow;

                    // TODO: 保存状态到Redis（持久化将在后续添加）

                    _logger.LogInformation("玩家离开房间: {PlayerId}, 房间: {RoomId}", 
                        _playerState.PlayerId, oldRoomId);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "玩家离开房间失败: {PlayerId}", playerId);
                    return false;
                }
            });
        }

        public async Task<string?> GetCurrentRoomAsync()
        {
            // 确保状态已初始化
            if (_playerState == null)
            {
                throw new InvalidOperationException("玩家状态未初始化");
            }

            return _playerState?.CurrentRoomId;
        }

        public async Task<bool> UpdateStatsAsync(PlayerStats stats)
        {
            var playerId = this.GetPrimaryKeyString();
            return await _distributedLock.WithPlayerLockAsync(playerId, async () =>
            {
                try
                {
                    // 确保状态已初始化
                    if (_playerState == null)
                    {
                        throw new InvalidOperationException("玩家状态未初始化");
                    }

                    if (_playerState == null)
                    {
                        return false;
                    }

                    _playerState.Stats = stats;
                    _playerState.LastActiveAt = DateTime.UtcNow;

                    // TODO: 保存状态到Redis（持久化将在后续添加）

                    _logger.LogInformation("玩家统计更新: {PlayerId}, 游戏场数: {GamesPlayed}", 
                        _playerState.PlayerId, stats.GamesPlayed);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "玩家统计更新失败: {PlayerId}", playerId);
                    return false;
                }
            });
        }

        public async Task<bool> UpdateSettingsAsync(PlayerSettings settings)
        {
            var playerId = this.GetPrimaryKeyString();
            return await _distributedLock.WithPlayerLockAsync(playerId, async () =>
            {
                try
                {
                    // 确保状态已初始化
                    if (_playerState == null)
                    {
                        throw new InvalidOperationException("玩家状态未初始化");
                    }

                    if (_playerState == null)
                    {
                        return false;
                    }

                    _playerState.Settings = settings;
                    _playerState.LastActiveAt = DateTime.UtcNow;

                    // TODO: 保存状态到Redis（持久化将在后续添加）

                    _logger.LogInformation("玩家设置更新: {PlayerId}", _playerState.PlayerId);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "玩家设置更新失败: {PlayerId}", playerId);
                    return false;
                }
            });
        }

        public async Task<bool> IsOnlineAsync()
        {
            // 确保状态已初始化
            if (_playerState == null)
            {
                throw new InvalidOperationException("玩家状态未初始化");
            }

            if (_playerState == null)
            {
                return false;
            }

            return _playerState.OnlineStatus != PlayerOnlineStatus.Offline;
        }

        public async Task<DateTime> GetLastActiveTimeAsync()
        {
            // 确保状态已初始化
            if (_playerState == null)
            {
                throw new InvalidOperationException("玩家状态未初始化");
            }

            if (_playerState == null)
            {
                return DateTime.MinValue;
            }

            return _playerState.LastActiveAt;
        }

        public async Task<bool> HeartbeatAsync()
        {
            var playerId = this.GetPrimaryKeyString();
            try
            {
                // 心跳是高频操作，使用tryLock避免阻塞
                var success = await _distributedLock.TryWithLockAsync(
                    $"Player:{playerId}:Heartbeat",
                    async () =>
                    {
                        // 确保状态已初始化
                        if (_playerState == null)
                        {
                            return;
                        }

                        _playerState.LastActiveAt = DateTime.UtcNow;
                        // 心跳不写入Redis，减少频繁写入
                        // await _playerState.WriteStateAsync();
                    },
                    expiry: TimeSpan.FromSeconds(10),
                    timeout: TimeSpan.FromSeconds(1)
                );

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "心跳更新失败: {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<bool> ValidateSessionAsync(string sessionId)
        {
            // 检查输入参数有效性
            if (string.IsNullOrEmpty(sessionId))
            {
                return false;
            }
            
            // 确保状态已初始化
            if (_playerState == null)
            {
                throw new InvalidOperationException("玩家状态未初始化");
            }

            if (_playerState == null)
            {
                return false;
            }
            
            return _playerState.Session.SessionId == sessionId && 
                   _playerState.Session.SessionExpireTime > DateTime.UtcNow;
        }

        public async Task<PlayerState?> GetFullStateAsync()
        {
            // 确保状态已初始化
            if (_playerState == null)
            {
                throw new InvalidOperationException("玩家状态未初始化");
            }

            return _playerState;
        }

        public async Task<bool> SaveStateAsync()
        {
            var playerId = this.GetPrimaryKeyString();
            try
            {
                // 确保状态已加载
                if (_playerState == null)
                {
                    // TODO: 从Redis读取状态（持久化将在后续添加）
                }

                if (_playerState == null)
                {
                    _logger.LogWarning("玩家状态不存在，无法保存: {PlayerId}", playerId);
                    return false;
                }

                // 强制保存到Redis
                // TODO: 保存状态到Redis（持久化将在后续添加）
                _logger.LogDebug("玩家状态强制保存: {PlayerId}", _playerState.PlayerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家状态保存失败: {PlayerId}", playerId);
                return false;
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
                Position = state.Position,
                CurrentRoomId = state.CurrentRoomId
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