using Microsoft.Extensions.Logging;
using Orleans;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

namespace Wind.Grains
{
    /// <summary>
    /// 房间Grain实现
    /// 负责管理单个房间的状态、玩家操作和游戏控制
    /// 临时使用内存状态，后续添加持久化
    /// </summary>
    public class RoomGrain : Grain, IRoomGrain
    {
        private readonly ILogger<RoomGrain> _logger;
        private RoomState? _roomState;
        private readonly object _lockObject = new object();

        public RoomGrain(ILogger<RoomGrain> logger)
        {
            _logger = logger;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var roomId = this.GetPrimaryKeyString();
            _logger.LogInformation("RoomGrain激活: {RoomId}", roomId);

            await base.OnActivateAsync(cancellationToken);
        }

        public async Task<CreateRoomResponse> CreateRoomAsync(CreateRoomRequest request)
        {
            try
            {
                var roomId = this.GetPrimaryKeyString();
                _logger.LogInformation("创建房间: {RoomId} by {CreatorId}", roomId, request.CreatorId);

                lock (_lockObject)
                {
                    if (_roomState != null)
                    {
                        return new CreateRoomResponse
                        {
                            Success = false,
                            Message = "房间已存在"
                        };
                    }

                    _roomState = new RoomState
                    {
                        RoomId = roomId,
                        RoomName = request.RoomName,
                        CreatorId = request.CreatorId,
                        RoomType = request.RoomType,
                        MaxPlayerCount = Math.Max(1, Math.Min(request.MaxPlayerCount, 16)), // 限制1-16人
                        Password = request.Password,
                        Settings = request.Settings,
                        CustomData = request.CustomData,
                        Status = RoomStatus.Waiting,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                }

                // 添加房间创建事件
                await AddRoomEventAsync(RoomEventType.GameStarted, request.CreatorId, $"房间 '{request.RoomName}' 创建成功");

                return new CreateRoomResponse
                {
                    Success = true,
                    Message = "房间创建成功",
                    RoomId = roomId,
                    RoomInfo = _roomState
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建房间失败: {RoomId}", this.GetPrimaryKeyString());
                return new CreateRoomResponse
                {
                    Success = false,
                    Message = $"创建房间失败: {ex.Message}"
                };
            }
        }

        public async Task<JoinRoomResponse> JoinRoomAsync(JoinRoomRequest request)
        {
            try
            {
                _logger.LogInformation("玩家加入房间: {PlayerId} -> {RoomId}", request.PlayerId, request.RoomId);

                if (!await IsExistsAsync())
                {
                    return new JoinRoomResponse
                    {
                        Success = false,
                        Message = "房间不存在"
                    };
                }

                RoomPlayer? newPlayer = null;
                
                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    // 检查密码
                    if (!string.IsNullOrEmpty(_roomState.Password) && _roomState.Password != request.Password)
                    {
                        return new JoinRoomResponse
                        {
                            Success = false,
                            Message = "房间密码错误"
                        };
                    }

                    // 检查房间状态
                    if (_roomState.Status == RoomStatus.InGame && !request.IsSpectator)
                    {
                        return new JoinRoomResponse
                        {
                            Success = false,
                            Message = "游戏已开始，无法加入"
                        };
                    }

                    if (_roomState.Status == RoomStatus.Closed)
                    {
                        return new JoinRoomResponse
                        {
                            Success = false,
                            Message = "房间已关闭"
                        };
                    }

                    // 检查是否已在房间中
                    var existingPlayer = _roomState.Players.FirstOrDefault(p => p.PlayerId == request.PlayerId);
                    if (existingPlayer != null)
                    {
                        return new JoinRoomResponse
                        {
                            Success = true,
                            Message = "已在房间中",
                            RoomInfo = _roomState,
                            PlayerInfo = existingPlayer
                        };
                    }

                    // 检查房间容量
                    if (!request.IsSpectator && _roomState.CurrentPlayerCount >= _roomState.MaxPlayerCount)
                    {
                        return new JoinRoomResponse
                        {
                            Success = false,
                            Message = "房间已满"
                        };
                    }

                    // 创建新玩家
                    newPlayer = new RoomPlayer
                    {
                        PlayerId = request.PlayerId,
                        DisplayName = request.PlayerData.ContainsKey("DisplayName") ? 
                            request.PlayerData["DisplayName"].ToString() ?? request.PlayerId : request.PlayerId,
                        Level = request.PlayerData.ContainsKey("Level") ? 
                            Convert.ToInt32(request.PlayerData["Level"]) : 1,
                        Role = _roomState.Players.Count == 0 ? PlayerRole.Leader : PlayerRole.Member,
                        ReadyStatus = PlayerReadyStatus.NotReady,
                        JoinedAt = DateTime.UtcNow,
                        PlayerData = request.PlayerData
                    };

                    if (!request.IsSpectator)
                    {
                        _roomState.Players.Add(newPlayer);
                        _roomState.CurrentPlayerCount = _roomState.Players.Count;
                    }
                    else
                    {
                        _roomState.GameState.Spectators.Add(request.PlayerId);
                    }

                    _roomState.UpdatedAt = DateTime.UtcNow;
                }

                // 添加玩家加入事件
                await AddRoomEventAsync(
                    RoomEventType.PlayerJoined, 
                    request.PlayerId, 
                    $"玩家 {newPlayer?.DisplayName ?? request.PlayerId} 加入房间"
                );

                return new JoinRoomResponse
                {
                    Success = true,
                    Message = "加入房间成功",
                    RoomInfo = _roomState,
                    PlayerInfo = newPlayer
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家加入房间失败: {PlayerId} -> {RoomId}", request.PlayerId, request.RoomId);
                return new JoinRoomResponse
                {
                    Success = false,
                    Message = $"加入房间失败: {ex.Message}"
                };
            }
        }

        public async Task<LeaveRoomResponse> LeaveRoomAsync(LeaveRoomRequest request)
        {
            try
            {
                _logger.LogInformation("玩家离开房间: {PlayerId} <- {RoomId}", request.PlayerId, request.RoomId);

                if (!await IsExistsAsync())
                {
                    return new LeaveRoomResponse
                    {
                        Success = false,
                        Message = "房间不存在"
                    };
                }

                string? playerName = null;
                bool wasLeader = false;

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    var player = _roomState.Players.FirstOrDefault(p => p.PlayerId == request.PlayerId);
                    if (player == null)
                    {
                        // 检查是否在观众列表中
                        if (_roomState.GameState.Spectators.Contains(request.PlayerId))
                        {
                            _roomState.GameState.Spectators.Remove(request.PlayerId);
                            return new LeaveRoomResponse
                            {
                                Success = true,
                                Message = "离开观众席成功"
                            };
                        }

                        return new LeaveRoomResponse
                        {
                            Success = false,
                            Message = "玩家不在房间中"
                        };
                    }

                    playerName = player.DisplayName;
                    wasLeader = player.Role == PlayerRole.Leader;

                    // 移除玩家
                    _roomState.Players.Remove(player);
                    _roomState.CurrentPlayerCount = _roomState.Players.Count;

                    // 如果房主离开，转移房主权限
                    if (wasLeader && _roomState.Players.Count > 0)
                    {
                        var newLeader = _roomState.Players.OrderBy(p => p.JoinedAt).First();
                        newLeader.Role = PlayerRole.Leader;
                        _logger.LogInformation("房主权限转移: {OldLeader} -> {NewLeader}", request.PlayerId, newLeader.PlayerId);
                    }

                    // 如果房间为空，标记为关闭
                    if (_roomState.Players.Count == 0)
                    {
                        _roomState.Status = RoomStatus.Closed;
                        _logger.LogInformation("房间因无玩家而关闭: {RoomId}", _roomState.RoomId);
                    }

                    _roomState.UpdatedAt = DateTime.UtcNow;
                }

                // 添加玩家离开事件
                await AddRoomEventAsync(
                    RoomEventType.PlayerLeft, 
                    request.PlayerId, 
                    $"玩家 {playerName} 离开房间" + (!string.IsNullOrEmpty(request.Reason) ? $": {request.Reason}" : "")
                );

                return new LeaveRoomResponse
                {
                    Success = true,
                    Message = "离开房间成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "玩家离开房间失败: {PlayerId} <- {RoomId}", request.PlayerId, request.RoomId);
                return new LeaveRoomResponse
                {
                    Success = false,
                    Message = $"离开房间失败: {ex.Message}"
                };
            }
        }

        public async Task<GetRoomInfoResponse> GetRoomInfoAsync(GetRoomInfoRequest request)
        {
            try
            {
                if (!await IsExistsAsync())
                {
                    return new GetRoomInfoResponse
                    {
                        Success = false,
                        Message = "房间不存在"
                    };
                }

                EnsureRoomStateInitialized();

                return new GetRoomInfoResponse
                {
                    Success = true,
                    Message = "获取房间信息成功",
                    RoomInfo = _roomState
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取房间信息失败: {RoomId}", request.RoomId);
                return new GetRoomInfoResponse
                {
                    Success = false,
                    Message = $"获取房间信息失败: {ex.Message}"
                };
            }
        }

        public async Task<UpdateRoomSettingsResponse> UpdateRoomSettingsAsync(UpdateRoomSettingsRequest request)
        {
            try
            {
                if (!await IsExistsAsync())
                {
                    return new UpdateRoomSettingsResponse
                    {
                        Success = false,
                        Message = "房间不存在"
                    };
                }

                if (!await HasPermissionAsync(request.PlayerId, RoomOperation.UpdateSettings))
                {
                    return new UpdateRoomSettingsResponse
                    {
                        Success = false,
                        Message = "无权限修改房间设置"
                    };
                }

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    _roomState.Settings = request.Settings;
                    _roomState.UpdatedAt = DateTime.UtcNow;
                }

                await AddRoomEventAsync(
                    RoomEventType.RoomSettingsChanged, 
                    request.PlayerId, 
                    "房间设置已更新"
                );

                return new UpdateRoomSettingsResponse
                {
                    Success = true,
                    Message = "房间设置更新成功",
                    UpdatedSettings = _roomState.Settings
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新房间设置失败: {RoomId}", request.RoomId);
                return new UpdateRoomSettingsResponse
                {
                    Success = false,
                    Message = $"更新房间设置失败: {ex.Message}"
                };
            }
        }

        public async Task<PlayerReadyResponse> SetPlayerReadyAsync(PlayerReadyRequest request)
        {
            try
            {
                if (!await IsExistsAsync())
                {
                    return new PlayerReadyResponse
                    {
                        Success = false,
                        Message = "房间不存在"
                    };
                }

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    var player = _roomState.Players.FirstOrDefault(p => p.PlayerId == request.PlayerId);
                    if (player == null)
                    {
                        return new PlayerReadyResponse
                        {
                            Success = false,
                            Message = "玩家不在房间中"
                        };
                    }

                    player.ReadyStatus = request.ReadyStatus;
                    _roomState.UpdatedAt = DateTime.UtcNow;
                }

                var eventType = request.ReadyStatus == PlayerReadyStatus.Ready ? 
                    RoomEventType.PlayerReady : RoomEventType.PlayerNotReady;
                
                await AddRoomEventAsync(
                    eventType, 
                    request.PlayerId, 
                    $"玩家准备状态: {request.ReadyStatus}"
                );

                return new PlayerReadyResponse
                {
                    Success = true,
                    Message = "准备状态更新成功",
                    ReadyStatus = request.ReadyStatus
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置玩家准备状态失败: {PlayerId} in {RoomId}", request.PlayerId, request.RoomId);
                return new PlayerReadyResponse
                {
                    Success = false,
                    Message = $"设置准备状态失败: {ex.Message}"
                };
            }
        }

        public async Task<StartGameResponse> StartGameAsync(StartGameRequest request)
        {
            try
            {
                if (!await IsExistsAsync())
                {
                    return new StartGameResponse
                    {
                        Success = false,
                        Message = "房间不存在"
                    };
                }

                if (!await HasPermissionAsync(request.PlayerId, RoomOperation.StartGame))
                {
                    return new StartGameResponse
                    {
                        Success = false,
                        Message = "无权限开始游戏"
                    };
                }

                if (!request.ForceStart && !await CanStartGameAsync())
                {
                    return new StartGameResponse
                    {
                        Success = false,
                        Message = "游戏开始条件不满足（玩家未全部准备或人数不足）"
                    };
                }

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    if (_roomState.Status == RoomStatus.InGame)
                    {
                        return new StartGameResponse
                        {
                            Success = false,
                            Message = "游戏已在进行中"
                        };
                    }

                    _roomState.Status = RoomStatus.InGame;
                    _roomState.GameStartTime = DateTime.UtcNow;
                    _roomState.GameState.RoundNumber = 1;
                    _roomState.GameState.ElapsedTime = 0;
                    _roomState.GameState.LastUpdateTime = DateTime.UtcNow;
                    _roomState.UpdatedAt = DateTime.UtcNow;

                    // 重置所有玩家分数
                    _roomState.GameState.PlayerScores.Clear();
                    foreach (var player in _roomState.Players)
                    {
                        _roomState.GameState.PlayerScores[player.PlayerId] = 0;
                        player.Score = 0;
                    }
                }

                await AddRoomEventAsync(
                    RoomEventType.GameStarted, 
                    request.PlayerId, 
                    "游戏开始"
                );

                return new StartGameResponse
                {
                    Success = true,
                    Message = "游戏开始成功",
                    GameStartTime = _roomState.GameStartTime,
                    GameState = _roomState.GameState
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "开始游戏失败: {RoomId}", request.RoomId);
                return new StartGameResponse
                {
                    Success = false,
                    Message = $"开始游戏失败: {ex.Message}"
                };
            }
        }

        public async Task<EndGameResponse> EndGameAsync(EndGameRequest request)
        {
            try
            {
                if (!await IsExistsAsync())
                {
                    return new EndGameResponse
                    {
                        Success = false,
                        Message = "房间不存在"
                    };
                }

                if (!await HasPermissionAsync(request.PlayerId, RoomOperation.EndGame))
                {
                    return new EndGameResponse
                    {
                        Success = false,
                        Message = "无权限结束游戏"
                    };
                }

                string? winner = null;
                DateTime gameEndTime = DateTime.UtcNow;

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    if (_roomState.Status != RoomStatus.InGame)
                    {
                        return new EndGameResponse
                        {
                            Success = false,
                            Message = "游戏未在进行中"
                        };
                    }

                    _roomState.Status = RoomStatus.Finished;
                    _roomState.GameEndTime = gameEndTime;
                    
                    // 更新最终分数
                    foreach (var score in request.FinalScores)
                    {
                        if (_roomState.GameState.PlayerScores.ContainsKey(score.Key))
                        {
                            _roomState.GameState.PlayerScores[score.Key] = score.Value;
                        }
                        
                        var player = _roomState.Players.FirstOrDefault(p => p.PlayerId == score.Key);
                        if (player != null)
                        {
                            player.Score = score.Value;
                        }
                    }

                    // 确定获胜者
                    if (_roomState.GameState.PlayerScores.Count > 0)
                    {
                        var maxScore = _roomState.GameState.PlayerScores.Values.Max();
                        winner = _roomState.GameState.PlayerScores
                            .FirstOrDefault(kvp => kvp.Value == maxScore).Key;
                        _roomState.GameState.CurrentWinner = winner;
                    }

                    _roomState.UpdatedAt = DateTime.UtcNow;

                    // 重置玩家准备状态
                    foreach (var player in _roomState.Players)
                    {
                        player.ReadyStatus = PlayerReadyStatus.NotReady;
                    }
                }

                await AddRoomEventAsync(
                    RoomEventType.GameEnded, 
                    request.PlayerId, 
                    $"游戏结束{(winner != null ? $"，获胜者: {winner}" : "")}"
                );

                return new EndGameResponse
                {
                    Success = true,
                    Message = "游戏结束",
                    GameEndTime = gameEndTime,
                    FinalScores = _roomState.GameState.PlayerScores,
                    Winner = winner
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "结束游戏失败: {RoomId}", request.RoomId);
                return new EndGameResponse
                {
                    Success = false,
                    Message = $"结束游戏失败: {ex.Message}"
                };
            }
        }

        public async Task<KickPlayerResponse> KickPlayerAsync(KickPlayerRequest request)
        {
            try
            {
                if (!await IsExistsAsync())
                {
                    return new KickPlayerResponse
                    {
                        Success = false,
                        Message = "房间不存在"
                    };
                }

                if (!await HasPermissionAsync(request.OperatorId, RoomOperation.KickPlayer))
                {
                    return new KickPlayerResponse
                    {
                        Success = false,
                        Message = "无权限踢出玩家"
                    };
                }

                if (request.OperatorId == request.TargetPlayerId)
                {
                    return new KickPlayerResponse
                    {
                        Success = false,
                        Message = "不能踢出自己"
                    };
                }

                string? targetPlayerName = null;

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    var targetPlayer = _roomState.Players.FirstOrDefault(p => p.PlayerId == request.TargetPlayerId);
                    if (targetPlayer == null)
                    {
                        return new KickPlayerResponse
                        {
                            Success = false,
                            Message = "目标玩家不在房间中"
                        };
                    }

                    targetPlayerName = targetPlayer.DisplayName;

                    _roomState.Players.Remove(targetPlayer);
                    _roomState.CurrentPlayerCount = _roomState.Players.Count;
                    _roomState.UpdatedAt = DateTime.UtcNow;
                }

                await AddRoomEventAsync(
                    RoomEventType.PlayerKicked, 
                    request.TargetPlayerId, 
                    $"玩家 {targetPlayerName} 被踢出房间" + (!string.IsNullOrEmpty(request.Reason) ? $": {request.Reason}" : "")
                );

                return new KickPlayerResponse
                {
                    Success = true,
                    Message = "踢出玩家成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "踢出玩家失败: {TargetPlayerId} from {RoomId}", request.TargetPlayerId, request.RoomId);
                return new KickPlayerResponse
                {
                    Success = false,
                    Message = $"踢出玩家失败: {ex.Message}"
                };
            }
        }

        public Task<List<RoomPlayer>> GetPlayersAsync()
        {
            EnsureRoomStateInitialized();
            return Task.FromResult(_roomState?.Players ?? new List<RoomPlayer>());
        }

        public Task<bool> IsExistsAsync()
        {
            return Task.FromResult(_roomState != null && _roomState.Status != RoomStatus.Closed);
        }

        public async Task<bool> UpdatePlayerPositionAsync(string playerId, PlayerPosition position)
        {
            try
            {
                if (!await IsExistsAsync())
                    return false;

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    var player = _roomState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                    if (player == null)
                        return false;

                    player.Position = position;
                    _roomState.UpdatedAt = DateTime.UtcNow;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新玩家位置失败: {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<bool> UpdatePlayerScoreAsync(string playerId, int score)
        {
            try
            {
                if (!await IsExistsAsync())
                    return false;

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    var player = _roomState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                    if (player == null)
                        return false;

                    player.Score = score;
                    _roomState.GameState.PlayerScores[playerId] = score;
                    _roomState.GameState.LastUpdateTime = DateTime.UtcNow;
                    _roomState.UpdatedAt = DateTime.UtcNow;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新玩家分数失败: {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<bool> AddRoomEventAsync(RoomEventType eventType, string? playerId, string description, Dictionary<string, object>? eventData = null)
        {
            try
            {
                if (!await IsExistsAsync())
                    return false;

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    var roomEvent = new RoomEvent
                    {
                        EventType = eventType,
                        PlayerId = playerId,
                        Description = description,
                        EventData = eventData ?? new Dictionary<string, object>()
                    };

                    _roomState.GameState.RecentEvents.Add(roomEvent);

                    // 只保留最近100个事件
                    if (_roomState.GameState.RecentEvents.Count > 100)
                    {
                        _roomState.GameState.RecentEvents = _roomState.GameState.RecentEvents
                            .OrderByDescending(e => e.Timestamp)
                            .Take(100)
                            .ToList();
                    }

                    _roomState.UpdatedAt = DateTime.UtcNow;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加房间事件失败: {EventType}", eventType);
                return false;
            }
        }

        public Task<List<RoomEvent>> GetRecentEventsAsync(int count = 50)
        {
            EnsureRoomStateInitialized();
            
            var events = _roomState?.GameState.RecentEvents
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList() ?? new List<RoomEvent>();
                
            return Task.FromResult(events);
        }

        public async Task<bool> CloseRoomAsync(string operatorId, string? reason = null)
        {
            try
            {
                if (!await IsExistsAsync())
                    return false;

                if (!await HasPermissionAsync(operatorId, RoomOperation.CloseRoom))
                    return false;

                lock (_lockObject)
                {
                    EnsureRoomStateInitialized();

                    _roomState.Status = RoomStatus.Closed;
                    _roomState.UpdatedAt = DateTime.UtcNow;
                }

                await AddRoomEventAsync(
                    RoomEventType.RoomClosed, 
                    operatorId, 
                    "房间已关闭" + (!string.IsNullOrEmpty(reason) ? $": {reason}" : "")
                );

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "关闭房间失败: {RoomId}", this.GetPrimaryKeyString());
                return false;
            }
        }

        public Task<bool> CanStartGameAsync()
        {
            EnsureRoomStateInitialized();

            if (_roomState == null)
                return Task.FromResult(false);

            // 检查基本条件
            if (_roomState.Status != RoomStatus.Waiting && _roomState.Status != RoomStatus.Ready)
                return Task.FromResult(false);

            if (_roomState.Players.Count < _roomState.Settings.MinPlayersToStart)
                return Task.FromResult(false);

            // 如果开启自动开始，只需检查人数
            if (_roomState.Settings.AutoStart)
                return Task.FromResult(true);

            // 否则检查所有玩家是否准备
            var allReady = _roomState.Players.All(p => p.ReadyStatus == PlayerReadyStatus.Ready);
            return Task.FromResult(allReady);
        }

        public Task<bool> HasPermissionAsync(string playerId, RoomOperation operation)
        {
            EnsureRoomStateInitialized();

            if (_roomState == null)
                return Task.FromResult(false);

            var player = _roomState.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null)
                return Task.FromResult(false);

            // 房主和管理员有所有权限
            if (player.Role == PlayerRole.Leader || player.Role == PlayerRole.Admin)
                return Task.FromResult(true);

            // 普通成员的权限较少
            return operation switch
            {
                RoomOperation.UpdateSettings => Task.FromResult(false),
                RoomOperation.StartGame => Task.FromResult(false),
                RoomOperation.EndGame => Task.FromResult(false),
                RoomOperation.KickPlayer => Task.FromResult(false),
                RoomOperation.CloseRoom => Task.FromResult(false),
                _ => Task.FromResult(false)
            };
        }

        public Task<RoomBrief?> GetRoomBriefAsync()
        {
            if (_roomState == null)
                return Task.FromResult<RoomBrief?>(null);

            var brief = new RoomBrief
            {
                RoomId = _roomState.RoomId,
                RoomName = _roomState.RoomName,
                RoomType = _roomState.RoomType,
                Status = _roomState.Status,
                CurrentPlayerCount = _roomState.CurrentPlayerCount,
                MaxPlayerCount = _roomState.MaxPlayerCount,
                HasPassword = !string.IsNullOrEmpty(_roomState.Password),
                CreatorName = _roomState.Players.FirstOrDefault(p => p.Role == PlayerRole.Leader)?.DisplayName ?? "Unknown",
                CreatedAt = _roomState.CreatedAt,
                GameMode = _roomState.Settings.GameMode,
                MapId = _roomState.Settings.MapId
            };

            return Task.FromResult<RoomBrief?>(brief);
        }

        private void EnsureRoomStateInitialized()
        {
            if (_roomState == null)
            {
                throw new InvalidOperationException("房间状态未初始化，请先创建房间");
            }
        }
    }
}