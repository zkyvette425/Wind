using Wind.Server.Models.Documents;
using Wind.Shared.Models;

namespace Wind.Server.Services;

/// <summary>
/// 房间数据持久化服务接口
/// 提供房间数据的MongoDB持久化操作
/// </summary>
public interface IRoomPersistenceService
{
    /// <summary>
    /// 保存房间数据到MongoDB
    /// </summary>
    Task<string> SaveRoomAsync(RoomState roomState);

    /// <summary>
    /// 批量保存房间数据
    /// </summary>
    Task<List<string>> SaveRoomsAsync(IEnumerable<RoomState> roomStates);

    /// <summary>
    /// 根据房间ID获取房间数据
    /// </summary>
    Task<RoomDocument?> GetRoomByIdAsync(string roomId);

    /// <summary>
    /// 搜索房间（按名称或描述）
    /// </summary>
    Task<List<RoomDocument>> SearchRoomsAsync(string searchText, int limit = 20);

    /// <summary>
    /// 获取活跃房间列表
    /// </summary>
    Task<List<RoomDocument>> GetActiveRoomsAsync(int limit = 100);

    /// <summary>
    /// 获取指定玩家创建的房间
    /// </summary>
    Task<List<RoomDocument>> GetRoomsByOwnerAsync(string ownerId, int limit = 50);

    /// <summary>
    /// 获取指定玩家参与的房间历史
    /// </summary>
    Task<List<RoomDocument>> GetRoomsByPlayerAsync(string playerId, int limit = 50);

    /// <summary>
    /// 获取指定游戏模式的房间
    /// </summary>
    Task<List<RoomDocument>> GetRoomsByGameModeAsync(string gameMode, int page = 1, int pageSize = 50);

    /// <summary>
    /// 获取房间统计排行榜（按游戏时长）
    /// </summary>
    Task<List<RoomDocument>> GetRoomRankingByDurationAsync(int page = 1, int pageSize = 50);

    /// <summary>
    /// 获取最近完成的游戏房间
    /// </summary>
    Task<List<RoomDocument>> GetRecentCompletedRoomsAsync(TimeSpan timeRange, int limit = 100);

    /// <summary>
    /// 获取今日创建的房间
    /// </summary>
    Task<List<RoomDocument>> GetTodayRoomsAsync(int limit = 100);

    /// <summary>
    /// 更新房间状态
    /// </summary>
    Task<bool> UpdateRoomStatusAsync(string roomId, RoomStatus status);

    /// <summary>
    /// 更新房间玩家数量
    /// </summary>
    Task<bool> UpdateRoomPlayerCountAsync(string roomId, int playerCount);

    /// <summary>
    /// 更新房间开始时间
    /// </summary>
    Task<bool> UpdateRoomStartTimeAsync(string roomId, DateTime startTime);

    /// <summary>
    /// 更新房间结束时间并计算持续时间
    /// </summary>
    Task<bool> UpdateRoomEndTimeAsync(string roomId, DateTime endTime);

    /// <summary>
    /// 添加玩家到房间
    /// </summary>
    Task<bool> AddPlayerToRoomAsync(string roomId, RoomPlayerDocument player);

    /// <summary>
    /// 从房间移除玩家
    /// </summary>
    Task<bool> RemovePlayerFromRoomAsync(string roomId, string playerId);

    /// <summary>
    /// 删除房间数据
    /// </summary>
    Task<bool> DeleteRoomAsync(string roomId);

    /// <summary>
    /// 获取房间数据统计信息
    /// </summary>
    Task<RoomDataStatistics> GetRoomStatisticsAsync();

    /// <summary>
    /// 获取游戏模式统计
    /// </summary>
    Task<Dictionary<string, GameModeStatistics>> GetGameModeStatisticsAsync();

    /// <summary>
    /// 清理过期的房间数据
    /// </summary>
    Task<int> CleanupExpiredRoomsAsync(TimeSpan expiredThreshold);
}

/// <summary>
/// 房间数据统计信息
/// </summary>
public class RoomDataStatistics
{
    public long TotalRooms { get; set; }
    public long ActiveRooms { get; set; }
    public long CompletedRooms { get; set; }
    public long RoomsCreatedToday { get; set; }
    public long RoomsCompletedToday { get; set; }
    public Dictionary<RoomStatus, long> RoomsByStatus { get; set; } = new();
    public Dictionary<RoomType, long> RoomsByType { get; set; } = new();
    public float AverageGameDuration { get; set; }
    public float AveragePlayersPerRoom { get; set; }
    public int MaxPlayersInRoom { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 游戏模式统计信息
/// </summary>
public class GameModeStatistics
{
    public string GameMode { get; set; } = string.Empty;
    public long TotalRooms { get; set; }
    public long CompletedRooms { get; set; }
    public float CompletionRate { get; set; }
    public float AverageDuration { get; set; }
    public float AveragePlayerCount { get; set; }
    public long TotalPlayersServed { get; set; }
    public DateTime LastPlayed { get; set; }
}