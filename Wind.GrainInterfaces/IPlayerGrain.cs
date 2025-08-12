using Orleans;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

namespace Wind.GrainInterfaces
{
    /// <summary>
    /// 玩家Grain接口
    /// 提供玩家状态管理、登录认证、位置更新等核心功能
    /// </summary>
    public interface IPlayerGrain : IGrainWithStringKey
    {
        /// <summary>
        /// 玩家登录
        /// </summary>
        /// <param name="request">登录请求信息</param>
        /// <returns>登录响应结果</returns>
        Task<PlayerLoginResponse> LoginAsync(PlayerLoginRequest request);

        /// <summary>
        /// 玩家登出
        /// </summary>
        /// <param name="request">登出请求信息</param>
        /// <returns>登出响应结果</returns>
        Task<PlayerLogoutResponse> LogoutAsync(PlayerLogoutRequest request);

        /// <summary>
        /// 获取玩家信息
        /// </summary>
        /// <param name="includeStats">是否包含统计信息</param>
        /// <param name="includeSettings">是否包含设置信息</param>
        /// <returns>玩家信息</returns>
        Task<PlayerInfo?> GetPlayerInfoAsync(bool includeStats = true, bool includeSettings = false);

        /// <summary>
        /// 更新玩家信息
        /// </summary>
        /// <param name="request">更新请求</param>
        /// <returns>更新响应结果</returns>
        Task<PlayerUpdateResponse> UpdatePlayerAsync(PlayerUpdateRequest request);

        /// <summary>
        /// 更新玩家位置
        /// </summary>
        /// <param name="position">新位置信息</param>
        /// <returns>更新是否成功</returns>
        Task<bool> UpdatePositionAsync(PlayerPosition position);

        /// <summary>
        /// 设置在线状态
        /// </summary>
        /// <param name="status">在线状态</param>
        /// <returns>设置是否成功</returns>
        Task<bool> SetOnlineStatusAsync(PlayerOnlineStatus status);

        /// <summary>
        /// 加入房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <returns>加入是否成功</returns>
        Task<bool> JoinRoomAsync(string roomId);

        /// <summary>
        /// 离开房间
        /// </summary>
        /// <returns>离开是否成功</returns>
        Task<bool> LeaveRoomAsync();

        /// <summary>
        /// 获取当前房间ID
        /// </summary>
        /// <returns>房间ID，如果不在房间中返回null</returns>
        Task<string?> GetCurrentRoomAsync();

        /// <summary>
        /// 更新玩家统计信息
        /// </summary>
        /// <param name="stats">统计信息</param>
        /// <returns>更新是否成功</returns>
        Task<bool> UpdateStatsAsync(PlayerStats stats);

        /// <summary>
        /// 更新玩家设置
        /// </summary>
        /// <param name="settings">设置信息</param>
        /// <returns>更新是否成功</returns>
        Task<bool> UpdateSettingsAsync(PlayerSettings settings);

        /// <summary>
        /// 检查玩家是否在线
        /// </summary>
        /// <returns>是否在线</returns>
        Task<bool> IsOnlineAsync();

        /// <summary>
        /// 获取最后活跃时间
        /// </summary>
        /// <returns>最后活跃时间</returns>
        Task<DateTime> GetLastActiveTimeAsync();

        /// <summary>
        /// 心跳更新，保持活跃状态
        /// </summary>
        /// <returns>心跳响应</returns>
        Task<bool> HeartbeatAsync();

        /// <summary>
        /// 验证会话有效性
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <returns>会话是否有效</returns>
        Task<bool> ValidateSessionAsync(string sessionId);

        /// <summary>
        /// 获取玩家的完整状态信息（仅内部使用）
        /// </summary>
        /// <returns>完整的玩家状态</returns>
        Task<PlayerState?> GetFullStateAsync();

        /// <summary>
        /// 强制保存状态到持久化存储
        /// </summary>
        /// <returns>保存是否成功</returns>
        Task<bool> SaveStateAsync();
    }
}