using MagicOnion;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

namespace Wind.Shared.Services
{
    /// <summary>
    /// 游戏管理MagicOnion Unary服务接口
    /// 提供游戏核心功能API，包括房间管理、匹配系统、游戏流程控制等
    /// </summary>
    public interface IGameService : IService<IGameService>
    {
        #region 房间管理API

        /// <summary>
        /// 创建游戏房间API
        /// </summary>
        /// <param name="request">创建房间请求</param>
        /// <returns>创建房间响应</returns>
        UnaryResult<CreateRoomResponse> CreateRoomAsync(CreateRoomRequest request);

        /// <summary>
        /// 获取房间信息API
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="includePlayerList">是否包含玩家列表</param>
        /// <returns>房间信息</returns>
        UnaryResult<GetRoomInfoResponse> GetRoomInfoAsync(string roomId, bool includePlayerList = true);

        /// <summary>
        /// 获取房间列表API
        /// </summary>
        /// <param name="request">房间列表请求</param>
        /// <returns>房间列表响应</returns>
        UnaryResult<GetRoomListResponse> GetRoomListAsync(GetRoomListRequest request);

        /// <summary>
        /// 更新房间设置API
        /// </summary>
        /// <param name="request">更新房间设置请求</param>
        /// <returns>更新响应</returns>
        UnaryResult<UpdateRoomSettingsResponse> UpdateRoomSettingsAsync(UpdateRoomSettingsRequest request);

        /// <summary>
        /// 解散房间API
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="ownerId">房主ID</param>
        /// <returns>解散房间响应</returns>
        UnaryResult<LeaveRoomResponse> DisbandRoomAsync(string roomId, string ownerId);

        #endregion

        #region 匹配系统API

        /// <summary>
        /// 快速匹配API
        /// </summary>
        /// <param name="request">快速匹配请求</param>
        /// <returns>匹配响应</returns>
        UnaryResult<QuickMatchResponse> QuickMatchAsync(QuickMatchRequest request);

        /// <summary>
        /// 加入匹配队列API
        /// </summary>
        /// <param name="request">匹配队列请求</param>
        /// <returns>匹配队列响应</returns>
        UnaryResult<JoinMatchmakingQueueResponse> JoinMatchmakingQueueAsync(JoinMatchmakingQueueRequest request);

        /// <summary>
        /// 取消匹配API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>取消匹配响应</returns>
        UnaryResult<CancelMatchmakingResponse> CancelMatchmakingAsync(string playerId);

        /// <summary>
        /// 获取匹配状态API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>匹配状态响应</returns>
        UnaryResult<GetMatchmakingStatusResponse> GetMatchmakingStatusAsync(string playerId);

        #endregion

        #region 游戏流程API

        /// <summary>
        /// 开始游戏API
        /// </summary>
        /// <param name="request">开始游戏请求</param>
        /// <returns>开始游戏响应</returns>
        UnaryResult<StartGameResponse> StartGameAsync(StartGameRequest request);

        /// <summary>
        /// 结束游戏API
        /// </summary>
        /// <param name="request">结束游戏请求</param>
        /// <returns>结束游戏响应</returns>
        UnaryResult<EndGameResponse> EndGameAsync(EndGameRequest request);

        /// <summary>
        /// 设置玩家准备状态API
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="isReady">是否准备</param>
        /// <returns>设置准备状态响应</returns>
        UnaryResult<PlayerReadyResponse> SetPlayerReadyAsync(string roomId, string playerId, bool isReady);

        #endregion

        #region 玩家管理API

        /// <summary>
        /// 踢出玩家API (房主权限)
        /// </summary>
        /// <param name="request">踢出玩家请求</param>
        /// <returns>踢出玩家响应</returns>
        UnaryResult<KickPlayerResponse> KickPlayerAsync(KickPlayerRequest request);

        #endregion
    }
}