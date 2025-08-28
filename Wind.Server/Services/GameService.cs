using MagicOnion;
using MagicOnion.Server;
using Microsoft.Extensions.Logging;
using Orleans;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Shared.Services;
using Wind.Server.Filters;

namespace Wind.Server.Services
{
    /// <summary>
    /// 游戏管理MagicOnion Unary服务实现
    /// 提供游戏核心功能API，包括房间管理、匹配系统、游戏流程控制等
    /// 将客户端请求桥接到对应的Orleans Grain
    /// </summary>
    public class GameService : ServiceBase<IGameService>, IGameService
    {
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger<GameService> _logger;

        public GameService(IGrainFactory grainFactory, ILogger<GameService> logger)
        {
            _grainFactory = grainFactory;
            _logger = logger;
        }

        #region 房间管理API实现

        /// <summary>
        /// 创建游戏房间API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<CreateRoomResponse> CreateRoomAsync(CreateRoomRequest request)
        {
            try
            {
                // 参数验证
                if (string.IsNullOrWhiteSpace(request.CreatorId))
                {
                    _logger.LogWarning("创建房间请求参数无效: OwnerId为空");
                    return new CreateRoomResponse
                    {
                        Success = false,
                        Message = "房主ID不能为空"
                    };
                }

                _logger.LogInformation("处理创建房间请求: CreatorId={CreatorId}, RoomName={RoomName}, RoomType={RoomType}",
                    request.CreatorId, request.RoomName, request.RoomType);

                // 生成房间ID
                var roomId = Guid.NewGuid().ToString();

                // 获取RoomGrain并创建房间
                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                var response = await roomGrain.CreateRoomAsync(request);

                // 如果房间创建成功，让房主加入房间
                if (response.Success && response.RoomInfo != null)
                {
                    try
                    {
                        var ownerGrain = _grainFactory.GetGrain<IPlayerGrain>(request.CreatorId);
                        await ownerGrain.JoinRoomAsync(roomId);
                        
                        _logger.LogInformation("房间创建成功并且房主已加入: RoomId={RoomId}, CreatorId={CreatorId}",
                            roomId, request.CreatorId);
                    }
                    catch (Exception joinEx)
                    {
                        _logger.LogWarning(joinEx, "房间创建成功但房主加入失败: RoomId={RoomId}, CreatorId={CreatorId}",
                            roomId, request.CreatorId);
                        // 不影响房间创建的成功状态
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建房间失败: CreatorId={CreatorId}", request.CreatorId);
                return new CreateRoomResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 获取房间信息API
        /// </summary>
        [HighFrequencyRateLimit]
        public async UnaryResult<GetRoomInfoResponse> GetRoomInfoAsync(string roomId, bool includePlayerList = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId))
                {
                    _logger.LogWarning("获取房间信息请求参数无效: RoomId为空");
                    return new GetRoomInfoResponse
                    {
                        Success = false,
                        Message = "房间ID不能为空"
                    };
                }

                _logger.LogDebug("获取房间信息: RoomId={RoomId}, IncludePlayerList={IncludePlayerList}",
                    roomId, includePlayerList);

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                
                // 检查房间是否存在
                var roomExists = await roomGrain.IsExistsAsync();
                if (!roomExists)
                {
                    return new GetRoomInfoResponse
                    {
                        Success = false,
                        Message = "房间不存在"
                    };
                }

                var request = new GetRoomInfoRequest
                {
                    RoomId = roomId,
                    IncludePlayerDetails = includePlayerList
                };

                var response = await roomGrain.GetRoomInfoAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取房间信息失败: RoomId={RoomId}", roomId);
                return new GetRoomInfoResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 获取房间列表API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<GetRoomListResponse> GetRoomListAsync(GetRoomListRequest request)
        {
            try
            {
                _logger.LogDebug("获取房间列表: RoomType={RoomType}, PageIndex={PageIndex}, PageSize={PageSize}",
                    request.RoomType, request.PageIndex, request.PageSize);

                // TODO: 实现房间列表服务
                // 这里需要一个RoomListGrain或者RoomManagerGrain来管理房间列表
                // 当前简化实现，返回空列表

                _logger.LogWarning("获取房间列表功能暂未实现，返回空列表");

                return new GetRoomListResponse
                {
                    Success = true,
                    Message = "房间列表获取成功",
                    Rooms = new List<RoomBrief>(),
                    TotalCount = 0,
                    PageIndex = request.PageIndex,
                    PageSize = request.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取房间列表失败");
                return new GetRoomListResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 更新房间设置API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<UpdateRoomSettingsResponse> UpdateRoomSettingsAsync(UpdateRoomSettingsRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RoomId) || string.IsNullOrWhiteSpace(request.PlayerId))
                {
                    return new UpdateRoomSettingsResponse
                    {
                        Success = false,
                        Message = "房间ID和操作者ID不能为空"
                    };
                }

                _logger.LogInformation("更新房间设置: RoomId={RoomId}, PlayerId={PlayerId}",
                    request.RoomId, request.PlayerId);

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(request.RoomId);
                
                // 检查操作权限
                var hasPermission = await roomGrain.HasPermissionAsync(request.PlayerId, RoomOperation.UpdateSettings);
                if (!hasPermission)
                {
                    return new UpdateRoomSettingsResponse
                    {
                        Success = false,
                        Message = "没有权限更新房间设置"
                    };
                }

                var response = await roomGrain.UpdateRoomSettingsAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新房间设置失败: RoomId={RoomId}", request.RoomId);
                return new UpdateRoomSettingsResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 解散房间API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<LeaveRoomResponse> DisbandRoomAsync(string roomId, string ownerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(ownerId))
                {
                    return new LeaveRoomResponse
                    {
                        Success = false,
                        Message = "房间ID和房主ID不能为空"
                    };
                }

                _logger.LogInformation("解散房间: RoomId={RoomId}, OwnerId={OwnerId}", roomId, ownerId);

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                
                // 检查操作权限
                var hasPermission = await roomGrain.HasPermissionAsync(ownerId, RoomOperation.CloseRoom);
                if (!hasPermission)
                {
                    return new LeaveRoomResponse
                    {
                        Success = false,
                        Message = "没有权限解散房间"
                    };
                }

                // 关闭房间
                var success = await roomGrain.CloseRoomAsync(ownerId, "房主解散房间");
                
                return new LeaveRoomResponse
                {
                    Success = success,
                    Message = success ? "房间解散成功" : "房间解散失败"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解散房间失败: RoomId={RoomId}, OwnerId={OwnerId}", roomId, ownerId);
                return new LeaveRoomResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        #endregion

        #region 匹配系统API实现

        /// <summary>
        /// 快速匹配API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<QuickMatchResponse> QuickMatchAsync(QuickMatchRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.PlayerId))
                {
                    return new QuickMatchResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("处理快速匹配请求: PlayerId={PlayerId}, PlayerLevel={PlayerLevel}",
                    request.PlayerId, request.PlayerLevel);

                // 获取匹配系统Grain
                var matchmakingGrain = _grainFactory.GetGrain<IMatchmakingGrain>("default");
                var response = await matchmakingGrain.QuickMatchAsync(request);

                _logger.LogInformation("快速匹配完成: PlayerId={PlayerId}, Success={Success}",
                    request.PlayerId, response.Success);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "快速匹配失败: PlayerId={PlayerId}", request.PlayerId);
                return new QuickMatchResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 加入匹配队列API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<JoinMatchmakingQueueResponse> JoinMatchmakingQueueAsync(JoinMatchmakingQueueRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.PlayerId))
                {
                    return new JoinMatchmakingQueueResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("玩家加入匹配队列: PlayerId={PlayerId}, QueueId={QueueId}",
                    request.PlayerId, request.QueueId);

                var matchmakingGrain = _grainFactory.GetGrain<IMatchmakingGrain>("default");
                var response = await matchmakingGrain.JoinQueueAsync(request);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加入匹配队列失败: PlayerId={PlayerId}", request.PlayerId);
                return new JoinMatchmakingQueueResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 取消匹配API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<CancelMatchmakingResponse> CancelMatchmakingAsync(string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new CancelMatchmakingResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogInformation("取消匹配: PlayerId={PlayerId}", playerId);

                var matchmakingGrain = _grainFactory.GetGrain<IMatchmakingGrain>("default");
                
                var request = new CancelMatchmakingRequest
                {
                    PlayerId = playerId,
                    RequestId = null // 取消当前所有匹配请求
                };
                
                var response = await matchmakingGrain.CancelMatchmakingAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消匹配失败: PlayerId={PlayerId}", playerId);
                return new CancelMatchmakingResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 获取匹配状态API
        /// </summary>
        [HighFrequencyRateLimit]
        public async UnaryResult<GetMatchmakingStatusResponse> GetMatchmakingStatusAsync(string playerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return new GetMatchmakingStatusResponse
                    {
                        Success = false,
                        Message = "玩家ID不能为空"
                    };
                }

                _logger.LogDebug("获取匹配状态: PlayerId={PlayerId}", playerId);

                var matchmakingGrain = _grainFactory.GetGrain<IMatchmakingGrain>("default");
                
                var request = new GetMatchmakingStatusRequest
                {
                    PlayerId = playerId
                };
                
                var response = await matchmakingGrain.GetMatchmakingStatusAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取匹配状态失败: PlayerId={PlayerId}", playerId);
                return new GetMatchmakingStatusResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        #endregion

        #region 游戏流程API实现

        /// <summary>
        /// 开始游戏API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<StartGameResponse> StartGameAsync(StartGameRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RoomId) || string.IsNullOrWhiteSpace(request.PlayerId))
                {
                    return new StartGameResponse
                    {
                        Success = false,
                        Message = "房间ID和发起者ID不能为空"
                    };
                }

                _logger.LogInformation("开始游戏: RoomId={RoomId}, PlayerId={PlayerId}",
                    request.RoomId, request.PlayerId);

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(request.RoomId);
                
                // 检查操作权限
                var hasPermission = await roomGrain.HasPermissionAsync(request.PlayerId, RoomOperation.StartGame);
                if (!hasPermission)
                {
                    return new StartGameResponse
                    {
                        Success = false,
                        Message = "没有权限开始游戏"
                    };
                }

                // 检查是否可以开始游戏
                var canStart = await roomGrain.CanStartGameAsync();
                if (!canStart)
                {
                    return new StartGameResponse
                    {
                        Success = false,
                        Message = "房间当前状态不允许开始游戏"
                    };
                }

                var response = await roomGrain.StartGameAsync(request);

                if (response.Success)
                {
                    _logger.LogInformation("游戏开始成功: RoomId={RoomId}", request.RoomId);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "开始游戏失败: RoomId={RoomId}", request.RoomId);
                return new StartGameResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 结束游戏API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<EndGameResponse> EndGameAsync(EndGameRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RoomId))
                {
                    return new EndGameResponse
                    {
                        Success = false,
                        Message = "房间ID不能为空"
                    };
                }

                _logger.LogInformation("结束游戏: RoomId={RoomId}, Reason={Reason}",
                    request.RoomId, request.Reason);

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(request.RoomId);
                
                // 如果指定了操作者，检查权限
                if (!string.IsNullOrWhiteSpace(request.PlayerId))
                {
                    var hasPermission = await roomGrain.HasPermissionAsync(request.PlayerId, RoomOperation.EndGame);
                    if (!hasPermission)
                    {
                        return new EndGameResponse
                        {
                            Success = false,
                            Message = "没有权限结束游戏"
                        };
                    }
                }

                var response = await roomGrain.EndGameAsync(request);

                if (response.Success)
                {
                    _logger.LogInformation("游戏结束成功: RoomId={RoomId}", request.RoomId);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "结束游戏失败: RoomId={RoomId}", request.RoomId);
                return new EndGameResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        /// <summary>
        /// 设置玩家准备状态API
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<PlayerReadyResponse> SetPlayerReadyAsync(string roomId, string playerId, bool isReady)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                {
                    return new PlayerReadyResponse
                    {
                        Success = false,
                        Message = "房间ID和玩家ID不能为空"
                    };
                }

                _logger.LogInformation("设置玩家准备状态: RoomId={RoomId}, PlayerId={PlayerId}, IsReady={IsReady}",
                    roomId, playerId, isReady);

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(roomId);
                
                var request = new PlayerReadyRequest
                {
                    RoomId = roomId,
                    PlayerId = playerId,
                    ReadyStatus = isReady ? PlayerReadyStatus.Ready : PlayerReadyStatus.NotReady
                };
                
                var response = await roomGrain.SetPlayerReadyAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置玩家准备状态失败: RoomId={RoomId}, PlayerId={PlayerId}",
                    roomId, playerId);
                return new PlayerReadyResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        #endregion

        #region 玩家管理API实现

        /// <summary>
        /// 踢出玩家API (房主权限)
        /// </summary>
        [StandardRateLimit]
        public async UnaryResult<KickPlayerResponse> KickPlayerAsync(KickPlayerRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RoomId) || 
                    string.IsNullOrWhiteSpace(request.TargetPlayerId) || 
                    string.IsNullOrWhiteSpace(request.OperatorId))
                {
                    return new KickPlayerResponse
                    {
                        Success = false,
                        Message = "房间ID、目标玩家ID和操作者ID不能为空"
                    };
                }

                _logger.LogInformation("踢出玩家: RoomId={RoomId}, TargetPlayerId={TargetPlayerId}, OperatorId={OperatorId}",
                    request.RoomId, request.TargetPlayerId, request.OperatorId);

                var roomGrain = _grainFactory.GetGrain<IRoomGrain>(request.RoomId);
                
                // 检查操作权限
                var hasPermission = await roomGrain.HasPermissionAsync(request.OperatorId, RoomOperation.KickPlayer);
                if (!hasPermission)
                {
                    return new KickPlayerResponse
                    {
                        Success = false,
                        Message = "没有权限踢出玩家"
                    };
                }

                var response = await roomGrain.KickPlayerAsync(request);

                // 如果踢出成功，更新被踢玩家的状态
                if (response.Success)
                {
                    try
                    {
                        var targetPlayerGrain = _grainFactory.GetGrain<IPlayerGrain>(request.TargetPlayerId);
                        await targetPlayerGrain.LeaveRoomAsync();
                        
                        _logger.LogInformation("玩家已被踢出并更新状态: TargetPlayerId={TargetPlayerId}",
                            request.TargetPlayerId);
                    }
                    catch (Exception playerEx)
                    {
                        _logger.LogWarning(playerEx, "玩家踢出成功但状态更新失败: TargetPlayerId={TargetPlayerId}",
                            request.TargetPlayerId);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "踢出玩家失败: RoomId={RoomId}, TargetPlayerId={TargetPlayerId}",
                    request.RoomId, request.TargetPlayerId);
                return new KickPlayerResponse
                {
                    Success = false,
                    Message = "内部服务器错误，请稍后重试"
                };
            }
        }

        #endregion
    }
}