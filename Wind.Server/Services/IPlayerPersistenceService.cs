using Wind.Server.Models.Documents;
using Wind.Shared.Models;

namespace Wind.Server.Services;

/// <summary>
/// 玩家数据持久化服务接口
/// 提供玩家数据的MongoDB持久化操作
/// </summary>
public interface IPlayerPersistenceService
{
    /// <summary>
    /// 保存玩家数据到MongoDB
    /// </summary>
    Task<string> SavePlayerAsync(PlayerState playerState);

    /// <summary>
    /// 批量保存玩家数据
    /// </summary>
    Task<List<string>> SavePlayersAsync(IEnumerable<PlayerState> playerStates);

    /// <summary>
    /// 根据玩家ID获取玩家数据
    /// </summary>
    Task<PlayerDocument?> GetPlayerByIdAsync(string playerId);

    /// <summary>
    /// 根据显示名称搜索玩家
    /// </summary>
    Task<List<PlayerDocument>> SearchPlayersByNameAsync(string displayName, int limit = 20);

    /// <summary>
    /// 获取在线玩家列表
    /// </summary>
    Task<List<PlayerDocument>> GetOnlinePlayersAsync(int limit = 100);

    /// <summary>
    /// 获取玩家排行榜（按等级）
    /// </summary>
    Task<List<PlayerDocument>> GetPlayerRankingByLevelAsync(int page = 1, int pageSize = 50);

    /// <summary>
    /// 获取玩家排行榜（按经验值）
    /// </summary>
    Task<List<PlayerDocument>> GetPlayerRankingByExperienceAsync(int page = 1, int pageSize = 50);

    /// <summary>
    /// 获取玩家游戏统计排行榜
    /// </summary>
    Task<List<PlayerDocument>> GetPlayerRankingByWinRateAsync(int page = 1, int pageSize = 50);

    /// <summary>
    /// 根据房间ID获取房间内玩家
    /// </summary>
    Task<List<PlayerDocument>> GetPlayersByRoomIdAsync(string roomId);

    /// <summary>
    /// 获取最近活跃的玩家
    /// </summary>
    Task<List<PlayerDocument>> GetRecentActivePlayersAsync(TimeSpan timeRange, int limit = 100);

    /// <summary>
    /// 获取新注册玩家
    /// </summary>
    Task<List<PlayerDocument>> GetNewPlayersAsync(DateTime since, int limit = 100);

    /// <summary>
    /// 更新玩家在线状态
    /// </summary>
    Task<bool> UpdatePlayerOnlineStatusAsync(string playerId, PlayerOnlineStatus status);

    /// <summary>
    /// 更新玩家当前房间
    /// </summary>
    Task<bool> UpdatePlayerCurrentRoomAsync(string playerId, string? roomId);

    /// <summary>
    /// 更新玩家统计数据
    /// </summary>
    Task<bool> UpdatePlayerStatsAsync(string playerId, PlayerStatsDocument stats);

    /// <summary>
    /// 删除玩家数据（软删除，添加删除标记）
    /// </summary>
    Task<bool> DeletePlayerAsync(string playerId);

    /// <summary>
    /// 获取玩家数据统计信息
    /// </summary>
    Task<PlayerDataStatistics> GetPlayerStatisticsAsync();

    /// <summary>
    /// 清理过期的离线玩家数据
    /// </summary>
    Task<int> CleanupInactivePlayersAsync(TimeSpan inactiveThreshold);
}

/// <summary>
/// 玩家数据统计信息
/// </summary>
public class PlayerDataStatistics
{
    public long TotalPlayers { get; set; }
    public long OnlinePlayers { get; set; }
    public long NewPlayersToday { get; set; }
    public long ActivePlayersToday { get; set; }
    public Dictionary<PlayerOnlineStatus, long> PlayersByStatus { get; set; } = new();
    public Dictionary<int, long> PlayersByLevel { get; set; } = new();
    public float AverageLevel { get; set; }
    public long TotalExperience { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}