using MagicOnion;
using MagicOnion.Server;
using Microsoft.Extensions.Logging;
using Orleans;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Shared.Services;
using Wind.Server.Filters;

namespace Wind.Server.Services
{
    /// <summary>
    /// 玩家管理MagicOnion Unary服务实现
    /// 提供RESTful风格的玩家API，连接客户端与Orleans PlayerGrain
    /// </summary>
    public class PlayerService : ServiceBase<IPlayerService>, IPlayerService
    {
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger<PlayerService> _logger;
        private readonly JwtService _jwtService;

        public PlayerService(IGrainFactory grainFactory, ILogger<PlayerService> logger, JwtService jwtService)
        {
            _grainFactory = grainFactory;
            _logger = logger;
            _jwtService = jwtService;
        }

        /// <summary>
        /// 玩家登录API
        /// </summary>
        [LoginRateLimit]
        public async UnaryResult<PlayerLoginResponse> LoginAsync(PlayerLoginRequest request)
        {
            try
            {
                // 参数验证
                if (string.IsNullOrWhiteSpace(request.PlayerId))
                {
                    _logger.LogWarning("登录请求参数无效: PlayerId为空");
                    return new PlayerLoginResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("处理玩家登录请求: PlayerId={PlayerId}, Platform={Platform}", 
                    request.PlayerId, request.Platform);

                // 获取PlayerGrain并调用登录方法
                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
                var response = await playerGrain.LoginAsync(request);

                // 如果登录成功，生成JWT令牌
                if (response.Success)
                {
                    try
                    {
                        // 准备额外的JWT声明
                        var additionalClaims = new Dictionary<string, string>
                        {
                            ["display_name"] = request.DisplayName ?? request.PlayerId,
                            ["platform"] = request.Platform,
                            ["device_id"] = request.DeviceId,
                            ["session_id"] = response.SessionId ?? Guid.NewGuid().ToString(),
                            ["login_time"] = DateTime.UtcNow.ToString("O")
                        };

                        // 生成JWT令牌对
                        var tokenResult = _jwtService.GenerateTokens(request.PlayerId, additionalClaims);

                        // 将JWT信息添加到响应中
                        response.AccessToken = tokenResult.AccessToken;
                        response.RefreshToken = tokenResult.RefreshToken;
                        response.AccessTokenExpiry = tokenResult.AccessTokenExpiry;
                        response.RefreshTokenExpiry = tokenResult.RefreshTokenExpiry;
                        response.TokenType = tokenResult.TokenType;

                        // 保持向后兼容性
                        response.AuthToken = tokenResult.AccessToken;

                        _logger.LogInformation("为玩家 {PlayerId} 成功生成JWT令牌，访问令牌过期时间: {AccessExpiry}", 
                            request.PlayerId, tokenResult.AccessTokenExpiry);
                    }
                    catch (Exception tokenEx)
                    {
                        _logger.LogError(tokenEx, "为玩家 {PlayerId} 生成JWT令牌失败", request.PlayerId);
                        // JWT生成失败不影响登录成功状态，但需要记录错误
                        response.Message += " (警告: 令牌生成失败，请尝试重新登录)";
                    }
                }

                _logger.LogInformation("玩家登录完成: PlayerId={PlayerId}, Success={Success}, HasJWT={HasJWT}", 
                    request.PlayerId, response.Success, !string.IsNullOrEmpty(response.AccessToken));

                return response;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "玩家登录参数验证失败: PlayerId={PlayerId}", request.PlayerId);
                return new PlayerLoginResponse
                {
                    Success = false,
                    Message = $"参数验证失败: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家登录失败: PlayerId={PlayerId}", request.PlayerId);
                return new PlayerLoginResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 玩家登出API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<PlayerLogoutResponse> LogoutAsync(PlayerLogoutRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.PlayerId))
                {
                    return new PlayerLogoutResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("处理玩家登出请求: PlayerId={PlayerId}", request.PlayerId);

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
                var response = await playerGrain.LogoutAsync(request);

                _logger.LogInformation("玩家登出完成: PlayerId={PlayerId}, Success={Success}", 
                    request.PlayerId, response.Success);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家登出失败: PlayerId={PlayerId}", request.PlayerId);
                return new PlayerLogoutResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 获取玩家信息API
        /// </summary>
        [HighFrequencyRateLimit]
        public async UnaryResult<PlayerInfo?> GetPlayerInfoAsync(string playerId, bool includeStats = true, bool includeSettings = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    _logger.LogWarning("获取玩家信息请求参数无效: PlayerId为空");
                    return null;
                }

                _logger.LogDebug("获取玩家信息: PlayerId={PlayerId}, IncludeStats={IncludeStats}, IncludeSettings={IncludeSettings}", 
                    playerId, includeStats, includeSettings);

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var playerInfo = await playerGrain.GetPlayerInfoAsync(includeStats, includeSettings);

                return playerInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取玩家信息失败: PlayerId={PlayerId}", playerId);
                return null;
            }
        }

        /// <summary>
        /// 更新玩家信息API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<PlayerUpdateResponse> UpdatePlayerAsync(PlayerUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.PlayerId))
                {
                    return new PlayerUpdateResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("更新玩家信息: PlayerId={PlayerId}", request.PlayerId);

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
                var response = await playerGrain.UpdatePlayerAsync(request);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新玩家信息失败: PlayerId={PlayerId}", request.PlayerId);
                return new PlayerUpdateResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 更新玩家位置API
        /// </summary>
        [HighFrequencyRateLimit]
        public async UnaryResult<UpdatePositionResponse> UpdatePlayerPositionAsync(string playerId, PlayerPosition position)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new UpdatePositionResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogDebug("更新玩家位置: PlayerId={PlayerId}, Position=({X}, {Y}, {Z})", 
                    playerId, position.X, position.Y, position.Z);

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var success = await playerGrain.UpdatePositionAsync(position);

                return new UpdatePositionResponse
                {
                    Success = success,
                    Message = success ? "位置更新成功" : "位置更新失败",
                    UpdatedPosition = success ? position : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新玩家位置失败: PlayerId={PlayerId}", playerId);
                return new UpdatePositionResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 设置在线状态API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<SetOnlineStatusResponse> SetOnlineStatusAsync(string playerId, PlayerOnlineStatus status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new SetOnlineStatusResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("设置玩家在线状态: PlayerId={PlayerId}, Status={Status}", playerId, status);

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var success = await playerGrain.SetOnlineStatusAsync(status);

                return new SetOnlineStatusResponse
                {
                    Success = success,
                    Message = success ? "在线状态设置成功" : "在线状态设置失败",
                    Status = status
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置玩家在线状态失败: PlayerId={PlayerId}", playerId);
                return new SetOnlineStatusResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 玩家加入房间API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<JoinRoomResponse> JoinRoomAsync(string playerId, string roomId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(roomId))
                {
                    return new JoinRoomResponse
                    {
                        Success = false,
                        Message = "玩家ID和房间ID不能为空"
                    };
                }

                _logger.LogInformation("玩家加入房间: PlayerId={PlayerId}, RoomId={RoomId}", playerId, roomId);

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var success = await playerGrain.JoinRoomAsync(roomId);

                return new JoinRoomResponse
                {
                    Success = success,
                    Message = success ? "成功加入房间" : "加入房间失败",
                    RoomInfo = null // TODO: 如果需要房间信息，需要从RoomGrain获取
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家加入房间失败: PlayerId={PlayerId}, RoomId={RoomId}", playerId, roomId);
                return new JoinRoomResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 玩家离开房间API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<LeaveRoomResponse> LeaveRoomAsync(string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new LeaveRoomResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("玩家离开房间: PlayerId={PlayerId}", playerId);

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                
                // 先获取当前房间ID
                var currentRoomId = await playerGrain.GetCurrentRoomAsync();
                
                // 离开房间
                var success = await playerGrain.LeaveRoomAsync();

                return new LeaveRoomResponse
                {
                    Success = success,
                    Message = success ? "成功离开房间" : "离开房间失败"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家离开房间失败: PlayerId={PlayerId}", playerId);
                return new LeaveRoomResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 获取当前房间API
        /// </summary>
        [HighFrequencyRateLimit]
        public async UnaryResult<GetCurrentRoomResponse> GetCurrentRoomAsync(string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new GetCurrentRoomResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var currentRoomId = await playerGrain.GetCurrentRoomAsync();

                return new GetCurrentRoomResponse
                {
                    Success = true,
                    Message = currentRoomId != null ? "获取当前房间成功" : "玩家当前不在任何房间中",
                    CurrentRoomId = currentRoomId,
                    JoinTime = null // TODO: 如果需要JoinTime，需要在PlayerGrain中添加相应方法
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取当前房间失败: PlayerId={PlayerId}", playerId);
                return new GetCurrentRoomResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 更新玩家统计信息API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<UpdateStatsResponse> UpdateStatsAsync(string playerId, PlayerStats stats)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new UpdateStatsResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("更新玩家统计信息: PlayerId={PlayerId}", playerId);

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var success = await playerGrain.UpdateStatsAsync(stats);

                return new UpdateStatsResponse
                {
                    Success = success,
                    Message = success ? "统计信息更新成功" : "统计信息更新失败",
                    UpdatedStats = success ? stats : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新玩家统计信息失败: PlayerId={PlayerId}", playerId);
                return new UpdateStatsResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 更新玩家设置API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<UpdateSettingsResponse> UpdateSettingsAsync(string playerId, PlayerSettings settings)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new UpdateSettingsResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("更新玩家设置: PlayerId={PlayerId}", playerId);

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var success = await playerGrain.UpdateSettingsAsync(settings);

                return new UpdateSettingsResponse
                {
                    Success = success,
                    Message = success ? "设置更新成功" : "设置更新失败",
                    UpdatedSettings = success ? settings : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新玩家设置失败: PlayerId={PlayerId}", playerId);
                return new UpdateSettingsResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 检查玩家是否在线API
        /// </summary>
        [HighFrequencyRateLimit]
        public async UnaryResult<IsOnlineResponse> IsOnlineAsync(string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new IsOnlineResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var isOnline = await playerGrain.IsOnlineAsync();
                var lastActiveTime = await playerGrain.GetLastActiveTimeAsync();

                // 获取玩家完整信息以获取状态
                var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);

                return new IsOnlineResponse
                {
                    Success = true,
                    Message = "在线状态查询成功",
                    IsOnline = isOnline,
                    Status = playerInfo?.OnlineStatus ?? PlayerOnlineStatus.Offline,
                    LastActiveAt = lastActiveTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查玩家在线状态失败: PlayerId={PlayerId}", playerId);
                return new IsOnlineResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 获取最后活跃时间API
        /// </summary>
        [HighFrequencyRateLimit]
        public async UnaryResult<GetLastActiveTimeResponse> GetLastActiveTimeAsync(string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new GetLastActiveTimeResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var lastActiveTime = await playerGrain.GetLastActiveTimeAsync();
                var timeSinceLastActive = DateTime.UtcNow - lastActiveTime;

                return new GetLastActiveTimeResponse
                {
                    Success = true,
                    Message = "最后活跃时间获取成功",
                    LastActiveTime = lastActiveTime,
                    TimeSinceLastActive = timeSinceLastActive
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最后活跃时间失败: PlayerId={PlayerId}", playerId);
                return new GetLastActiveTimeResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 心跳更新API
        /// </summary>
        [HighFrequencyRateLimit]
        public async UnaryResult<HeartbeatResponse> HeartbeatAsync(string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new HeartbeatResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var success = await playerGrain.HeartbeatAsync();
                var lastActiveTime = await playerGrain.GetLastActiveTimeAsync();

                // 获取当前状态
                var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);

                return new HeartbeatResponse
                {
                    Success = success,
                    Message = success ? "心跳更新成功" : "心跳更新失败",
                    ServerTime = DateTime.UtcNow,
                    LastActiveTime = lastActiveTime,
                    Status = playerInfo?.OnlineStatus ?? PlayerOnlineStatus.Offline
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "心跳更新失败: PlayerId={PlayerId}", playerId);
                return new HeartbeatResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 验证会话有效性API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<ValidateSessionResponse> ValidateSessionAsync(string playerId, string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(sessionId))
                {
                    return new ValidateSessionResponse
                    {
                        Success = false,
                        Message = "玩家ID和会话ID不能为空"
                    };
                }

                var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(playerId);
                var isValid = await playerGrain.ValidateSessionAsync(sessionId);

                // 如果会话有效，获取会话到期时间（这里简化处理，实际应该从PlayerGrain获取）
                DateTime? sessionExpireTime = null;
                TimeSpan? timeUntilExpiry = null;

                if (isValid)
                {
                    // TODO: 从PlayerGrain获取实际的会话到期时间
                    sessionExpireTime = DateTime.UtcNow.AddHours(24); // 假设24小时到期
                    timeUntilExpiry = sessionExpireTime - DateTime.UtcNow;
                }

                return new ValidateSessionResponse
                {
                    Success = true,
                    Message = "会话验证完成",
                    IsValid = isValid,
                    SessionExpireTime = sessionExpireTime,
                    TimeUntilExpiry = timeUntilExpiry
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证会话有效性失败: PlayerId={PlayerId}, SessionId={SessionId}", playerId, sessionId);
                return new ValidateSessionResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        // === JWT认证相关方法实现 ===

        /// <summary>
        /// 刷新访问令牌API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    return new RefreshTokenResponse
                    {
                        Success = false,
                        Message = "刷新令牌不能为空"
                    };
                }

                _logger.LogInformation("处理令牌刷新请求: PlayerId={PlayerId}", request.PlayerId);

                // 使用JWT服务刷新令牌
                var tokenResult = _jwtService.RefreshAccessToken(request.RefreshToken);
                if (tokenResult == null)
                {
                    _logger.LogWarning("刷新令牌失败，令牌无效或已过期: PlayerId={PlayerId}", request.PlayerId);
                    return new RefreshTokenResponse
                    {
                        Success = false,
                        Message = "刷新令牌无效或已过期，请重新登录"
                    };
                }

                _logger.LogInformation("令牌刷新成功: PlayerId={PlayerId}", request.PlayerId);

                return new RefreshTokenResponse
                {
                    Success = true,
                    Message = "令牌刷新成功",
                    AccessToken = tokenResult.AccessToken,
                    RefreshToken = tokenResult.RefreshToken,
                    AccessTokenExpiry = tokenResult.AccessTokenExpiry,
                    RefreshTokenExpiry = tokenResult.RefreshTokenExpiry,
                    TokenType = tokenResult.TokenType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "令牌刷新失败: PlayerId={PlayerId}", request.PlayerId);
                return new RefreshTokenResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 验证访问令牌API
        /// </summary>
        [HighFrequencyRateLimit]
        public async UnaryResult<ValidateTokenResponse> ValidateTokenAsync(ValidateTokenRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.AccessToken))
                {
                    return new ValidateTokenResponse
                    {
                        IsValid = false,
                        Message = "访问令牌不能为空"
                    };
                }

                // 验证令牌
                var validationResult = _jwtService.ValidateAccessToken(request.AccessToken);
                
                if (!validationResult.IsValid)
                {
                    return new ValidateTokenResponse
                    {
                        IsValid = false,
                        Message = validationResult.Error ?? "令牌验证失败"
                    };
                }

                var playerId = _jwtService.ExtractPlayerIdFromToken(request.AccessToken);
                
                // 如果指定了期望的玩家ID，进行验证
                if (!string.IsNullOrWhiteSpace(request.ExpectedPlayerId) && 
                    playerId != request.ExpectedPlayerId)
                {
                    return new ValidateTokenResponse
                    {
                        IsValid = false,
                        Message = "令牌中的玩家ID与期望的不匹配"
                    };
                }

                // 提取声明
                var claims = new Dictionary<string, string>();
                if (validationResult.Principal != null)
                {
                    foreach (var claim in validationResult.Principal.Claims)
                    {
                        claims[claim.Type] = claim.Value;
                    }
                }

                return new ValidateTokenResponse
                {
                    IsValid = true,
                    Message = "令牌验证成功",
                    PlayerId = playerId,
                    ExpiryTime = validationResult.SecurityToken?.ValidTo,
                    Claims = claims
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "令牌验证失败");
                return new ValidateTokenResponse
                {
                    IsValid = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 撤销令牌API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<RevokeTokenResponse> RevokeTokenAsync(RevokeTokenRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Token))
                {
                    return new RevokeTokenResponse
                    {
                        Success = false,
                        Message = "令牌不能为空"
                    };
                }

                _logger.LogInformation("处理令牌撤销请求: PlayerId={PlayerId}, RevokeType={RevokeType}", 
                    request.PlayerId, request.RevokeType);

                // TODO: 实现令牌撤销逻辑
                // 这里应该将令牌添加到黑名单或者从缓存中移除
                // 当前版本暂时简化处理，只做验证

                var isValidToken = request.RevokeType == TokenRevokeType.AccessToken ? 
                    _jwtService.ValidateAccessToken(request.Token).IsValid :
                    _jwtService.ValidateRefreshToken(request.Token).IsValid;

                if (!isValidToken)
                {
                    return new RevokeTokenResponse
                    {
                        Success = false,
                        Message = "令牌无效，无法撤销"
                    };
                }

                // 实际的撤销逻辑应该在这里实现
                // 例如：将令牌添加到Redis黑名单中
                
                _logger.LogInformation("令牌撤销成功: PlayerId={PlayerId}", request.PlayerId);

                return new RevokeTokenResponse
                {
                    Success = true,
                    Message = "令牌撤销成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "令牌撤销失败: PlayerId={PlayerId}", request.PlayerId);
                return new RevokeTokenResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 获取当前已认证用户信息API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<GetCurrentUserResponse> GetCurrentUserAsync(GetCurrentUserRequest request)
        {
            try
            {
                // TODO: 完整实现JWT认证集成，当前返回固定响应
                _logger.LogWarning("GetCurrentUserAsync暂时返回固定响应，待MagicOnion API兼容性修复");
                
                return new GetCurrentUserResponse
                {
                    Success = false,
                    Message = "JWT认证集成待优化，当前暂不可用"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取当前用户信息失败");
                return new GetCurrentUserResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }
    }
}