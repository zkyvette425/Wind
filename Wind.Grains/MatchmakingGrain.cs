using Microsoft.Extensions.Logging;
using Orleans;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

namespace Wind.Grains
{
    /// <summary>
    /// 匹配系统Grain实现
    /// 负责管理玩家匹配、队列管理、匹配算法和统计信息
    /// 临时使用内存状态，后续添加持久化
    /// </summary>
    public class MatchmakingGrain : Grain, IMatchmakingGrain
    {
        private readonly ILogger<MatchmakingGrain> _logger;
        private MatchmakingState? _matchmakingState;
        private readonly object _lockObject = new object();
        private Timer? _matchCheckTimer;

        public MatchmakingGrain(ILogger<MatchmakingGrain> logger)
        {
            _logger = logger;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var matchmakingId = this.GetPrimaryKeyString();
            _logger.LogInformation("MatchmakingGrain激活: {MatchmakingId}", matchmakingId);

            // 初始化匹配系统
            await InitializeAsync(new MatchmakingSettings());

            // 启动定时匹配检查
            _matchCheckTimer = new Timer(
                async _ => await TriggerMatchCheckAsync(),
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5)
            );

            await base.OnActivateAsync(cancellationToken);
        }

        public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _matchCheckTimer?.Dispose();
            await base.OnDeactivateAsync(reason, cancellationToken);
        }

        public async Task<QuickMatchResponse> QuickMatchAsync(QuickMatchRequest request)
        {
            try
            {
                _logger.LogInformation("快速匹配请求: {PlayerId}", request.PlayerId);

                EnsureMatchmakingStateInitialized();

                // 检查玩家是否已有匹配请求
                var existingRequest = await GetPlayerRequestAsync(request.PlayerId);
                if (existingRequest != null)
                {
                    return new QuickMatchResponse
                    {
                        Success = false,
                        Message = "玩家已在匹配队列中",
                        RequestId = existingRequest.RequestId
                    };
                }

                // 选择最合适的队列（简单策略：选择人数最多的活跃队列）
                var bestQueue = GetBestQueueForCriteria(request.Criteria);
                if (bestQueue == null)
                {
                    // 创建默认队列
                    await CreateQueueAsync("default", "默认队列", request.Criteria.PreferredRoomType, request.Criteria.PreferredGameMode);
                    bestQueue = _matchmakingState.Queues["default"];
                }

                // 创建匹配请求
                var matchRequest = new MatchmakingRequest
                {
                    PlayerId = request.PlayerId,
                    PlayerName = request.PlayerName,
                    PlayerLevel = request.PlayerLevel,
                    QueueId = bestQueue.QueueId,
                    Criteria = request.Criteria,
                    PlayerData = request.PlayerData
                };

                lock (_lockObject)
                {
                    bestQueue.WaitingPlayers.Add(matchRequest);
                    bestQueue.TotalPlayersInQueue = bestQueue.WaitingPlayers.Count;
                    _matchmakingState.ActiveRequests[matchRequest.RequestId] = matchRequest;
                    _matchmakingState.Statistics.CurrentPlayersInQueue++;
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;
                }

                // 立即尝试匹配
                var matchResult = await TryMatchInQueue(bestQueue.QueueId);
                
                if (matchResult?.Success == true && matchResult.MatchedPlayerIds.Contains(request.PlayerId))
                {
                    return new QuickMatchResponse
                    {
                        Success = true,
                        Message = "匹配成功",
                        RequestId = matchRequest.RequestId,
                        Result = matchResult,
                        EstimatedWaitTime = 0
                    };
                }

                // 估算等待时间
                var estimatedWaitTime = (int)bestQueue.AverageWaitTime.TotalSeconds;

                return new QuickMatchResponse
                {
                    Success = true,
                    Message = "已加入匹配队列",
                    RequestId = matchRequest.RequestId,
                    EstimatedWaitTime = estimatedWaitTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "快速匹配失败: {PlayerId}", request.PlayerId);
                return new QuickMatchResponse
                {
                    Success = false,
                    Message = $"匹配失败: {ex.Message}"
                };
            }
        }

        public async Task<JoinMatchmakingQueueResponse> JoinQueueAsync(JoinMatchmakingQueueRequest request)
        {
            try
            {
                _logger.LogInformation("加入匹配队列: {PlayerId} -> {QueueId}", request.PlayerId, request.QueueId);

                EnsureMatchmakingStateInitialized();

                if (!_matchmakingState.Queues.ContainsKey(request.QueueId))
                {
                    return new JoinMatchmakingQueueResponse
                    {
                        Success = false,
                        Message = "指定的队列不存在"
                    };
                }

                var queue = _matchmakingState.Queues[request.QueueId];
                if (!queue.IsActive)
                {
                    return new JoinMatchmakingQueueResponse
                    {
                        Success = false,
                        Message = "队列已禁用"
                    };
                }

                // 检查玩家是否已在队列中
                var existingRequest = await GetPlayerRequestAsync(request.PlayerId);
                if (existingRequest != null)
                {
                    return new JoinMatchmakingQueueResponse
                    {
                        Success = false,
                        Message = "玩家已在匹配队列中",
                        RequestId = existingRequest.RequestId
                    };
                }

                // 检查队列容量
                if (queue.WaitingPlayers.Count >= _matchmakingState.Settings.MaxQueueSize)
                {
                    return new JoinMatchmakingQueueResponse
                    {
                        Success = false,
                        Message = "队列已满"
                    };
                }

                // 创建匹配请求
                var matchRequest = new MatchmakingRequest
                {
                    PlayerId = request.PlayerId,
                    PlayerName = request.PlayerData.ContainsKey("PlayerName") ? 
                        request.PlayerData["PlayerName"].ToString() ?? request.PlayerId : request.PlayerId,
                    PlayerLevel = request.PlayerData.ContainsKey("PlayerLevel") ? 
                        Convert.ToInt32(request.PlayerData["PlayerLevel"]) : 1,
                    QueueId = request.QueueId,
                    Criteria = request.Criteria,
                    PlayerData = request.PlayerData
                };

                lock (_lockObject)
                {
                    queue.WaitingPlayers.Add(matchRequest);
                    queue.TotalPlayersInQueue = queue.WaitingPlayers.Count;
                    _matchmakingState.ActiveRequests[matchRequest.RequestId] = matchRequest;
                    _matchmakingState.Statistics.CurrentPlayersInQueue++;
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;
                }

                return new JoinMatchmakingQueueResponse
                {
                    Success = true,
                    Message = "加入队列成功",
                    RequestId = matchRequest.RequestId,
                    QueuePosition = queue.WaitingPlayers.Count,
                    EstimatedWaitTime = (int)queue.AverageWaitTime.TotalSeconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加入匹配队列失败: {PlayerId} -> {QueueId}", request.PlayerId, request.QueueId);
                return new JoinMatchmakingQueueResponse
                {
                    Success = false,
                    Message = $"加入队列失败: {ex.Message}"
                };
            }
        }

        public async Task<CancelMatchmakingResponse> CancelMatchmakingAsync(CancelMatchmakingRequest request)
        {
            try
            {
                _logger.LogInformation("取消匹配: {PlayerId}", request.PlayerId);

                var matchRequest = await GetPlayerRequestAsync(request.PlayerId);
                if (matchRequest == null)
                {
                    return new CancelMatchmakingResponse
                    {
                        Success = false,
                        Message = "玩家没有活跃的匹配请求"
                    };
                }

                lock (_lockObject)
                {
                    // 从队列中移除
                    if (_matchmakingState.Queues.ContainsKey(matchRequest.QueueId))
                    {
                        var queue = _matchmakingState.Queues[matchRequest.QueueId];
                        queue.WaitingPlayers.Remove(matchRequest);
                        queue.TotalPlayersInQueue = queue.WaitingPlayers.Count;
                    }

                    // 从活跃请求中移除
                    _matchmakingState.ActiveRequests.Remove(matchRequest.RequestId);
                    _matchmakingState.Statistics.CurrentPlayersInQueue--;
                    _matchmakingState.Statistics.CancelledRequests++;
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;

                    // 更新请求状态
                    matchRequest.Status = MatchmakingRequestStatus.Cancelled;
                }

                return new CancelMatchmakingResponse
                {
                    Success = true,
                    Message = "取消匹配成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消匹配失败: {PlayerId}", request.PlayerId);
                return new CancelMatchmakingResponse
                {
                    Success = false,
                    Message = $"取消匹配失败: {ex.Message}"
                };
            }
        }

        public Task<GetMatchmakingStatusResponse> GetMatchmakingStatusAsync(GetMatchmakingStatusRequest request)
        {
            try
            {
                var matchRequest = _matchmakingState?.ActiveRequests.Values
                    .FirstOrDefault(r => r.PlayerId == request.PlayerId);

                if (matchRequest == null)
                {
                    return Task.FromResult(new GetMatchmakingStatusResponse
                    {
                        Success = false,
                        Message = "玩家没有活跃的匹配请求"
                    });
                }

                var queue = _matchmakingState.Queues.GetValueOrDefault(matchRequest.QueueId);
                var queuePosition = queue?.WaitingPlayers.FindIndex(r => r.RequestId == matchRequest.RequestId) + 1 ?? 0;
                var estimatedTime = Math.Max(0, (int)(queue?.AverageWaitTime.TotalSeconds ?? 0) - (int)matchRequest.CurrentWaitTime.TotalSeconds);

                return Task.FromResult(new GetMatchmakingStatusResponse
                {
                    Success = true,
                    Message = "获取状态成功",
                    Request = matchRequest,
                    QueuePosition = queuePosition,
                    CurrentWaitTime = matchRequest.CurrentWaitTime,
                    EstimatedRemainingTime = estimatedTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取匹配状态失败: {PlayerId}", request.PlayerId);
                return Task.FromResult(new GetMatchmakingStatusResponse
                {
                    Success = false,
                    Message = $"获取状态失败: {ex.Message}"
                });
            }
        }

        public Task<GetMatchmakingQueuesResponse> GetQueuesAsync(GetMatchmakingQueuesRequest request)
        {
            try
            {
                EnsureMatchmakingStateInitialized();

                var queues = _matchmakingState.Queues.Values
                    .Where(q => request.IncludeInactive || q.IsActive)
                    .Where(q => request.FilterRoomType == null || q.RoomType == request.FilterRoomType)
                    .Where(q => string.IsNullOrEmpty(request.FilterGameMode) || q.GameMode == request.FilterGameMode)
                    .Select(q => new MatchmakingQueueInfo
                    {
                        QueueId = q.QueueId,
                        QueueName = q.QueueName,
                        RoomType = q.RoomType,
                        GameMode = q.GameMode,
                        PlayersInQueue = q.TotalPlayersInQueue,
                        AverageWaitTime = q.AverageWaitTime,
                        IsActive = q.IsActive,
                        Settings = q.QueueSettings
                    })
                    .ToList();

                return Task.FromResult(new GetMatchmakingQueuesResponse
                {
                    Success = true,
                    Message = "获取队列列表成功",
                    Queues = queues
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取队列列表失败");
                return Task.FromResult(new GetMatchmakingQueuesResponse
                {
                    Success = false,
                    Message = $"获取队列列表失败: {ex.Message}"
                });
            }
        }

        public Task<GetMatchmakingStatisticsResponse> GetStatisticsAsync(GetMatchmakingStatisticsRequest request)
        {
            try
            {
                EnsureMatchmakingStateInitialized();

                var response = new GetMatchmakingStatisticsResponse
                {
                    Success = true,
                    Message = "获取统计信息成功",
                    Statistics = _matchmakingState.Statistics
                };

                if (request.IncludeQueueDetails)
                {
                    response.QueueDetails = _matchmakingState.Queues.Values
                        .ToDictionary(
                            q => q.QueueId,
                            q => new MatchmakingQueueInfo
                            {
                                QueueId = q.QueueId,
                                QueueName = q.QueueName,
                                RoomType = q.RoomType,
                                GameMode = q.GameMode,
                                PlayersInQueue = q.TotalPlayersInQueue,
                                AverageWaitTime = q.AverageWaitTime,
                                IsActive = q.IsActive,
                                Settings = q.QueueSettings
                            });
                }

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取统计信息失败");
                return Task.FromResult(new GetMatchmakingStatisticsResponse
                {
                    Success = false,
                    Message = $"获取统计信息失败: {ex.Message}"
                });
            }
        }

        public Task<bool> InitializeAsync(MatchmakingSettings settings)
        {
            try
            {
                var matchmakingId = this.GetPrimaryKeyString();

                _matchmakingState = new MatchmakingState
                {
                    MatchmakingId = matchmakingId,
                    Settings = settings,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // 创建默认队列
                CreateQueueAsync("default", "默认队列", RoomType.Normal, "Default").Wait();
                CreateQueueAsync("ranked", "排位队列", RoomType.Ranked, "Ranked").Wait();

                _logger.LogInformation("匹配系统初始化完成: {MatchmakingId}", matchmakingId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "匹配系统初始化失败");
                return Task.FromResult(false);
            }
        }

        public Task<bool> CreateQueueAsync(string queueId, string queueName, RoomType roomType, string gameMode, MatchmakingQueueSettings? settings = null)
        {
            try
            {
                EnsureMatchmakingStateInitialized();

                if (_matchmakingState.Queues.ContainsKey(queueId))
                {
                    return Task.FromResult(false);
                }

                var queue = new MatchmakingQueue
                {
                    QueueId = queueId,
                    QueueName = queueName,
                    RoomType = roomType,
                    GameMode = gameMode,
                    QueueSettings = settings ?? new MatchmakingQueueSettings(),
                    IsActive = true
                };

                lock (_lockObject)
                {
                    _matchmakingState.Queues[queueId] = queue;
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;
                }

                _logger.LogInformation("创建匹配队列: {QueueId} - {QueueName}", queueId, queueName);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建匹配队列失败: {QueueId}", queueId);
                return Task.FromResult(false);
            }
        }

        public Task<bool> RemoveQueueAsync(string queueId)
        {
            try
            {
                EnsureMatchmakingStateInitialized();

                if (!_matchmakingState.Queues.ContainsKey(queueId))
                {
                    return Task.FromResult(false);
                }

                lock (_lockObject)
                {
                    var queue = _matchmakingState.Queues[queueId];
                    
                    // 取消队列中的所有请求
                    foreach (var request in queue.WaitingPlayers)
                    {
                        request.Status = MatchmakingRequestStatus.Cancelled;
                        _matchmakingState.ActiveRequests.Remove(request.RequestId);
                        _matchmakingState.Statistics.CancelledRequests++;
                        _matchmakingState.Statistics.CurrentPlayersInQueue--;
                    }

                    _matchmakingState.Queues.Remove(queueId);
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;
                }

                _logger.LogInformation("删除匹配队列: {QueueId}", queueId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除匹配队列失败: {QueueId}", queueId);
                return Task.FromResult(false);
            }
        }

        public Task<bool> UpdateQueueSettingsAsync(string queueId, MatchmakingQueueSettings settings)
        {
            try
            {
                EnsureMatchmakingStateInitialized();

                if (!_matchmakingState.Queues.ContainsKey(queueId))
                {
                    return Task.FromResult(false);
                }

                lock (_lockObject)
                {
                    _matchmakingState.Queues[queueId].QueueSettings = settings;
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新队列设置失败: {QueueId}", queueId);
                return Task.FromResult(false);
            }
        }

        public Task<bool> SetQueueActiveAsync(string queueId, bool isActive)
        {
            try
            {
                EnsureMatchmakingStateInitialized();

                if (!_matchmakingState.Queues.ContainsKey(queueId))
                {
                    return Task.FromResult(false);
                }

                lock (_lockObject)
                {
                    _matchmakingState.Queues[queueId].IsActive = isActive;
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;
                }

                _logger.LogInformation("队列状态更新: {QueueId} -> {IsActive}", queueId, isActive);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新队列状态失败: {QueueId}", queueId);
                return Task.FromResult(false);
            }
        }

        public async Task<int> TriggerMatchCheckAsync(string? queueId = null)
        {
            try
            {
                EnsureMatchmakingStateInitialized();
                int totalMatches = 0;

                var queuesToCheck = string.IsNullOrEmpty(queueId) 
                    ? _matchmakingState.Queues.Values.Where(q => q.IsActive)
                    : new[] { _matchmakingState.Queues.GetValueOrDefault(queueId) }.Where(q => q != null);

                foreach (var queue in queuesToCheck)
                {
                    var matchResult = await TryMatchInQueue(queue.QueueId);
                    if (matchResult?.Success == true)
                    {
                        totalMatches++;
                    }
                }

                return totalMatches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发匹配检查失败");
                return 0;
            }
        }

        public Task<int> CleanupExpiredRequestsAsync()
        {
            try
            {
                EnsureMatchmakingStateInitialized();
                int cleanedCount = 0;

                lock (_lockObject)
                {
                    var expiredRequests = _matchmakingState.ActiveRequests.Values
                        .Where(r => r.CurrentWaitTime > _matchmakingState.Settings.RequestTimeout)
                        .ToList();

                    foreach (var request in expiredRequests)
                    {
                        // 从队列中移除
                        if (_matchmakingState.Queues.ContainsKey(request.QueueId))
                        {
                            var queue = _matchmakingState.Queues[request.QueueId];
                            queue.WaitingPlayers.Remove(request);
                            queue.TotalPlayersInQueue = queue.WaitingPlayers.Count;
                        }

                        // 更新状态
                        request.Status = MatchmakingRequestStatus.Timeout;
                        _matchmakingState.ActiveRequests.Remove(request.RequestId);
                        _matchmakingState.Statistics.TimeoutRequests++;
                        _matchmakingState.Statistics.CurrentPlayersInQueue--;
                        cleanedCount++;
                    }

                    if (cleanedCount > 0)
                    {
                        _matchmakingState.UpdatedAt = DateTime.UtcNow;
                        _logger.LogInformation("清理过期匹配请求: {Count}个", cleanedCount);
                    }
                }

                return Task.FromResult(cleanedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期请求失败");
                return Task.FromResult(0);
            }
        }

        public Task<MatchmakingRequest?> GetPlayerRequestAsync(string playerId)
        {
            EnsureMatchmakingStateInitialized();
            
            var request = _matchmakingState.ActiveRequests.Values
                .FirstOrDefault(r => r.PlayerId == playerId);
                
            return Task.FromResult(request);
        }

        public Task<int> GetQueuePlayerCountAsync(string queueId)
        {
            EnsureMatchmakingStateInitialized();
            
            var count = _matchmakingState.Queues.GetValueOrDefault(queueId)?.TotalPlayersInQueue ?? 0;
            return Task.FromResult(count);
        }

        public Task<TimeSpan> GetQueueAverageWaitTimeAsync(string queueId)
        {
            EnsureMatchmakingStateInitialized();
            
            var waitTime = _matchmakingState.Queues.GetValueOrDefault(queueId)?.AverageWaitTime ?? TimeSpan.Zero;
            return Task.FromResult(waitTime);
        }

        public Task<bool> ResetStatisticsAsync()
        {
            try
            {
                EnsureMatchmakingStateInitialized();

                lock (_lockObject)
                {
                    _matchmakingState.Statistics = new MatchmakingStatistics
                    {
                        CurrentPlayersInQueue = _matchmakingState.Statistics.CurrentPlayersInQueue,
                        LastResetTime = DateTime.UtcNow
                    };
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;
                }

                _logger.LogInformation("匹配统计信息已重置");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重置统计信息失败");
                return Task.FromResult(false);
            }
        }

        public Task<MatchmakingHealthStatus> GetHealthStatusAsync()
        {
            try
            {
                EnsureMatchmakingStateInitialized();

                var status = new MatchmakingHealthStatus
                {
                    IsHealthy = true,
                    SystemStatus = "Healthy",
                    TotalActiveQueues = _matchmakingState.Queues.Values.Count(q => q.IsActive),
                    TotalPlayersInQueues = _matchmakingState.Statistics.CurrentPlayersInQueue,
                    TotalActiveRequests = _matchmakingState.ActiveRequests.Count,
                    Uptime = DateTime.UtcNow - _matchmakingState.CreatedAt
                };

                // 检查健康状况
                if (status.TotalPlayersInQueues > _matchmakingState.Settings.MaxQueueSize * 0.9)
                {
                    status.Issues.Add("队列接近容量限制");
                }

                if (status.TotalActiveRequests != status.TotalPlayersInQueues)
                {
                    status.Issues.Add("活跃请求数与队列玩家数不匹配");
                }

                status.IsHealthy = status.Issues.Count == 0;
                if (!status.IsHealthy)
                {
                    status.SystemStatus = "Warning";
                }

                return Task.FromResult(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取健康状态失败");
                return Task.FromResult(new MatchmakingHealthStatus
                {
                    IsHealthy = false,
                    SystemStatus = "Error",
                    Issues = { $"获取健康状态失败: {ex.Message}" }
                });
            }
        }

        public Task<bool> UpdateSettingsAsync(MatchmakingSettings settings)
        {
            try
            {
                EnsureMatchmakingStateInitialized();

                lock (_lockObject)
                {
                    _matchmakingState.Settings = settings;
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新匹配设置失败");
                return Task.FromResult(false);
            }
        }

        public async Task<bool> ForceRemovePlayerRequestAsync(string playerId, string reason)
        {
            try
            {
                var request = await GetPlayerRequestAsync(playerId);
                if (request == null)
                {
                    return false;
                }

                lock (_lockObject)
                {
                    // 从队列中移除
                    if (_matchmakingState.Queues.ContainsKey(request.QueueId))
                    {
                        var queue = _matchmakingState.Queues[request.QueueId];
                        queue.WaitingPlayers.Remove(request);
                        queue.TotalPlayersInQueue = queue.WaitingPlayers.Count;
                    }

                    // 从活跃请求中移除
                    _matchmakingState.ActiveRequests.Remove(request.RequestId);
                    _matchmakingState.Statistics.CurrentPlayersInQueue--;
                    _matchmakingState.UpdatedAt = DateTime.UtcNow;

                    request.Status = MatchmakingRequestStatus.Cancelled;
                }

                _logger.LogInformation("强制移除玩家匹配请求: {PlayerId}, 原因: {Reason}", playerId, reason);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "强制移除玩家请求失败: {PlayerId}", playerId);
                return false;
            }
        }

        #region 私有方法

        private void EnsureMatchmakingStateInitialized()
        {
            if (_matchmakingState == null)
            {
                throw new InvalidOperationException("匹配系统状态未初始化");
            }
        }

        private MatchmakingQueue? GetBestQueueForCriteria(MatchmakingCriteria criteria)
        {
            EnsureMatchmakingStateInitialized();

            return _matchmakingState.Queues.Values
                .Where(q => q.IsActive)
                .Where(q => q.RoomType == criteria.PreferredRoomType)
                .Where(q => string.IsNullOrEmpty(criteria.PreferredGameMode) || q.GameMode == criteria.PreferredGameMode)
                .OrderByDescending(q => q.TotalPlayersInQueue)
                .FirstOrDefault();
        }

        private async Task<MatchmakingResult?> TryMatchInQueue(string queueId)
        {
            try
            {
                if (!_matchmakingState.Queues.ContainsKey(queueId))
                {
                    return null;
                }

                var queue = _matchmakingState.Queues[queueId];
                if (queue.WaitingPlayers.Count < queue.QueueSettings.MinPlayersPerMatch)
                {
                    return null;
                }

                // 简单匹配算法：按等级差异分组
                var matchedPlayers = new List<MatchmakingRequest>();
                var remainingPlayers = queue.WaitingPlayers.OrderBy(p => p.RequestedAt).ToList();

                while (remainingPlayers.Count >= queue.QueueSettings.MinPlayersPerMatch && 
                       matchedPlayers.Count < queue.QueueSettings.MaxPlayersPerMatch)
                {
                    var basePlayer = remainingPlayers.First();
                    var compatiblePlayers = FindCompatiblePlayers(basePlayer, remainingPlayers, queue.QueueSettings);

                    if (compatiblePlayers.Count >= queue.QueueSettings.MinPlayersPerMatch)
                    {
                        var playersToMatch = compatiblePlayers.Take(queue.QueueSettings.MaxPlayersPerMatch).ToList();
                        matchedPlayers.AddRange(playersToMatch);
                        
                        foreach (var player in playersToMatch)
                        {
                            remainingPlayers.Remove(player);
                        }
                        break;
                    }
                    else
                    {
                        remainingPlayers.Remove(basePlayer);
                    }
                }

                if (matchedPlayers.Count >= queue.QueueSettings.MinPlayersPerMatch)
                {
                    return await CreateMatchForPlayers(matchedPlayers, queue);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "队列匹配失败: {QueueId}", queueId);
                return null;
            }
        }

        private List<MatchmakingRequest> FindCompatiblePlayers(MatchmakingRequest basePlayer, List<MatchmakingRequest> availablePlayers, MatchmakingQueueSettings settings)
        {
            var compatiblePlayers = new List<MatchmakingRequest> { basePlayer };

            var levelDifference = settings.LevelDifferenceThreshold;
            
            // 如果等待时间超过阈值，放宽等级限制
            if (basePlayer.CurrentWaitTime.TotalSeconds > settings.ExpandLevelDifferenceAfter)
            {
                levelDifference *= 2;
            }

            foreach (var player in availablePlayers)
            {
                if (player.RequestId == basePlayer.RequestId)
                    continue;

                // 检查等级差异
                if (Math.Abs(player.PlayerLevel - basePlayer.PlayerLevel) > levelDifference)
                    continue;

                // 检查游戏模式
                if (player.Criteria.PreferredGameMode != basePlayer.Criteria.PreferredGameMode)
                    continue;

                // 检查地区（如果启用）
                if (settings.EnableRegionPriority && 
                    !string.IsNullOrEmpty(player.Criteria.PreferredRegion) && 
                    !string.IsNullOrEmpty(basePlayer.Criteria.PreferredRegion) &&
                    player.Criteria.PreferredRegion != basePlayer.Criteria.PreferredRegion)
                    continue;

                compatiblePlayers.Add(player);

                if (compatiblePlayers.Count >= settings.MaxPlayersPerMatch)
                    break;
            }

            return compatiblePlayers;
        }

        private async Task<MatchmakingResult> CreateMatchForPlayers(List<MatchmakingRequest> players, MatchmakingQueue queue)
        {
            try
            {
                // 尝试找到现有房间或创建新房间
                var roomId = await FindOrCreateRoom(players, queue);
                
                if (string.IsNullOrEmpty(roomId))
                {
                    return new MatchmakingResult
                    {
                        Success = false,
                        Message = "无法创建或找到合适的房间",
                        ResultType = MatchmakingResultType.MatchFailed
                    };
                }

                // 更新匹配状态
                lock (_lockObject)
                {
                    foreach (var player in players)
                    {
                        player.Status = MatchmakingRequestStatus.Matched;
                        player.MatchedAt = DateTime.UtcNow;
                        player.MatchedRoomId = roomId;
                        
                        // 从队列中移除
                        queue.WaitingPlayers.Remove(player);
                        _matchmakingState.ActiveRequests.Remove(player.RequestId);
                    }

                    queue.TotalPlayersInQueue = queue.WaitingPlayers.Count;
                    queue.LastMatchTime = DateTime.UtcNow;

                    // 更新统计信息
                    _matchmakingState.Statistics.TotalMatchesMade++;
                    _matchmakingState.Statistics.TotalPlayersMatched += players.Count;
                    _matchmakingState.Statistics.CurrentPlayersInQueue -= players.Count;

                    // 更新平均匹配时间
                    var avgWaitTime = players.Average(p => p.CurrentWaitTime.TotalSeconds);
                    _matchmakingState.Statistics.AverageMatchTime = TimeSpan.FromSeconds(
                        (_matchmakingState.Statistics.AverageMatchTime.TotalSeconds + avgWaitTime) / 2);

                    _matchmakingState.UpdatedAt = DateTime.UtcNow;
                }

                _logger.LogInformation("匹配成功: {PlayerCount}名玩家匹配到房间 {RoomId}", players.Count, roomId);

                return new MatchmakingResult
                {
                    Success = true,
                    Message = "匹配成功",
                    ResultType = MatchmakingResultType.JoinedExistingRoom, // 或 CreatedNewRoom
                    RoomId = roomId,
                    MatchedPlayerIds = players.Select(p => p.PlayerId).ToList(),
                    WaitTime = TimeSpan.FromSeconds(players.Average(p => p.CurrentWaitTime.TotalSeconds))
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建匹配失败");
                return new MatchmakingResult
                {
                    Success = false,
                    Message = $"创建匹配失败: {ex.Message}",
                    ResultType = MatchmakingResultType.MatchFailed
                };
            }
        }

        private async Task<string?> FindOrCreateRoom(List<MatchmakingRequest> players, MatchmakingQueue queue)
        {
            try
            {
                // 简单实现：总是创建新房间
                var roomId = Guid.NewGuid().ToString();
                var roomGrain = GrainFactory.GetGrain<IRoomGrain>(roomId);

                var firstPlayer = players.First();
                var createRequest = new CreateRoomRequest
                {
                    CreatorId = firstPlayer.PlayerId,
                    RoomName = $"匹配房间_{DateTime.Now:HHmmss}",
                    RoomType = queue.RoomType,
                    MaxPlayerCount = queue.QueueSettings.MaxPlayersPerMatch,
                    Settings = new RoomSettings
                    {
                        GameMode = queue.GameMode,
                        MinPlayersToStart = queue.QueueSettings.MinPlayersPerMatch,
                        AutoStart = true
                    }
                };

                var createResponse = await roomGrain.CreateRoomAsync(createRequest);
                if (!createResponse.Success)
                {
                    _logger.LogError("创建房间失败: {Message}", createResponse.Message);
                    return null;
                }

                // 将其他玩家添加到房间
                foreach (var player in players.Skip(1))
                {
                    var joinRequest = new JoinRoomRequest
                    {
                        PlayerId = player.PlayerId,
                        RoomId = roomId,
                        PlayerData = player.PlayerData
                    };

                    var joinResponse = await roomGrain.JoinRoomAsync(joinRequest);
                    if (!joinResponse.Success)
                    {
                        _logger.LogWarning("玩家加入房间失败: {PlayerId} -> {RoomId}, {Message}", 
                            player.PlayerId, roomId, joinResponse.Message);
                    }
                }

                return roomId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查找或创建房间失败");
                return null;
            }
        }

        #endregion
    }
}