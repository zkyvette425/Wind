using MagicOnion;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

namespace Wind.Shared.Services
{
    /// <summary>
    /// 玩家管理MagicOnion Unary服务接口
    /// 提供RESTful风格的玩家API，支持登录、查询、更新等核心功能
    /// </summary>
    public interface IPlayerService : IService<IPlayerService>
    {
        /// <summary>
        /// 玩家登录API
        /// </summary>
        /// <param name="request">登录请求信息</param>
        /// <returns>登录响应结果</returns>
        UnaryResult<PlayerLoginResponse> LoginAsync(PlayerLoginRequest request);

        /// <summary>
        /// 玩家登出API
        /// </summary>
        /// <param name="request">登出请求信息</param>
        /// <returns>登出响应结果</returns>
        UnaryResult<PlayerLogoutResponse> LogoutAsync(PlayerLogoutRequest request);

        /// <summary>
        /// 获取玩家信息API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="includeStats">是否包含统计信息</param>
        /// <param name="includeSettings">是否包含设置信息</param>
        /// <returns>玩家信息，如果不存在返回null</returns>
        UnaryResult<PlayerInfo?> GetPlayerInfoAsync(string playerId, bool includeStats = true, bool includeSettings = false);

        /// <summary>
        /// 更新玩家信息API
        /// </summary>
        /// <param name="request">更新请求</param>
        /// <returns>更新响应结果</returns>
        UnaryResult<PlayerUpdateResponse> UpdatePlayerAsync(PlayerUpdateRequest request);

        /// <summary>
        /// 更新玩家位置API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="position">新位置信息</param>
        /// <returns>位置更新响应</returns>
        UnaryResult<UpdatePositionResponse> UpdatePlayerPositionAsync(string playerId, PlayerPosition position);

        /// <summary>
        /// 设置在线状态API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="status">在线状态</param>
        /// <returns>状态设置响应</returns>
        UnaryResult<SetOnlineStatusResponse> SetOnlineStatusAsync(string playerId, PlayerOnlineStatus status);

        /// <summary>
        /// 玩家加入房间API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="roomId">房间ID</param>
        /// <returns>加入房间响应</returns>
        UnaryResult<JoinRoomResponse> JoinRoomAsync(string playerId, string roomId);

        /// <summary>
        /// 玩家离开房间API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>离开房间响应</returns>
        UnaryResult<LeaveRoomResponse> LeaveRoomAsync(string playerId);

        /// <summary>
        /// 获取当前房间API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>当前房间响应</returns>
        UnaryResult<GetCurrentRoomResponse> GetCurrentRoomAsync(string playerId);

        /// <summary>
        /// 更新玩家统计信息API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="stats">统计信息</param>
        /// <returns>统计更新响应</returns>
        UnaryResult<UpdateStatsResponse> UpdateStatsAsync(string playerId, PlayerStats stats);

        /// <summary>
        /// 更新玩家设置API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="settings">设置信息</param>
        /// <returns>设置更新响应</returns>
        UnaryResult<UpdateSettingsResponse> UpdateSettingsAsync(string playerId, PlayerSettings settings);

        /// <summary>
        /// 检查玩家是否在线API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>在线状态响应</returns>
        UnaryResult<IsOnlineResponse> IsOnlineAsync(string playerId);

        /// <summary>
        /// 获取最后活跃时间API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>最后活跃时间响应</returns>
        UnaryResult<GetLastActiveTimeResponse> GetLastActiveTimeAsync(string playerId);

        /// <summary>
        /// 心跳更新API
        /// 保持玩家活跃状态，防止会话超时
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>心跳响应</returns>
        UnaryResult<HeartbeatResponse> HeartbeatAsync(string playerId);

        /// <summary>
        /// 验证会话有效性API
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="sessionId">会话ID</param>
        /// <returns>会话验证响应</returns>
        UnaryResult<ValidateSessionResponse> ValidateSessionAsync(string playerId, string sessionId);
    }
}