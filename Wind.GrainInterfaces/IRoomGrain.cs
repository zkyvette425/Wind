using Orleans;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

namespace Wind.GrainInterfaces
{
    /// <summary>
    /// 房间Grain接口
    /// 提供房间管理、玩家操作、游戏控制等核心功能
    /// </summary>
    public interface IRoomGrain : IGrainWithStringKey
    {
        /// <summary>
        /// 创建房间
        /// </summary>
        /// <param name="request">创建房间请求</param>
        /// <returns>创建结果</returns>
        Task<CreateRoomResponse> CreateRoomAsync(CreateRoomRequest request);

        /// <summary>
        /// 玩家加入房间
        /// </summary>
        /// <param name="request">加入房间请求</param>
        /// <returns>加入结果</returns>
        Task<JoinRoomResponse> JoinRoomAsync(JoinRoomRequest request);

        /// <summary>
        /// 玩家离开房间
        /// </summary>
        /// <param name="request">离开房间请求</param>
        /// <returns>离开结果</returns>
        Task<LeaveRoomResponse> LeaveRoomAsync(LeaveRoomRequest request);

        /// <summary>
        /// 获取房间信息
        /// </summary>
        /// <param name="request">房间信息请求</param>
        /// <returns>房间信息</returns>
        Task<GetRoomInfoResponse> GetRoomInfoAsync(GetRoomInfoRequest request);

        /// <summary>
        /// 更新房间设置
        /// </summary>
        /// <param name="request">设置更新请求</param>
        /// <returns>更新结果</returns>
        Task<UpdateRoomSettingsResponse> UpdateRoomSettingsAsync(UpdateRoomSettingsRequest request);

        /// <summary>
        /// 设置玩家准备状态
        /// </summary>
        /// <param name="request">准备状态请求</param>
        /// <returns>设置结果</returns>
        Task<PlayerReadyResponse> SetPlayerReadyAsync(PlayerReadyRequest request);

        /// <summary>
        /// 开始游戏
        /// </summary>
        /// <param name="request">开始游戏请求</param>
        /// <returns>开始结果</returns>
        Task<StartGameResponse> StartGameAsync(StartGameRequest request);

        /// <summary>
        /// 结束游戏
        /// </summary>
        /// <param name="request">结束游戏请求</param>
        /// <returns>结束结果</returns>
        Task<EndGameResponse> EndGameAsync(EndGameRequest request);

        /// <summary>
        /// 踢出玩家
        /// </summary>
        /// <param name="request">踢出玩家请求</param>
        /// <returns>踢出结果</returns>
        Task<KickPlayerResponse> KickPlayerAsync(KickPlayerRequest request);

        /// <summary>
        /// 获取房间当前玩家列表
        /// </summary>
        /// <returns>玩家列表</returns>
        Task<List<RoomPlayer>> GetPlayersAsync();

        /// <summary>
        /// 检查房间是否存在
        /// </summary>
        /// <returns>是否存在</returns>
        Task<bool> IsExistsAsync();

        /// <summary>
        /// 更新玩家在房间内的位置
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="position">新位置</param>
        /// <returns>更新是否成功</returns>
        Task<bool> UpdatePlayerPositionAsync(string playerId, PlayerPosition position);

        /// <summary>
        /// 更新玩家分数
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="score">新分数</param>
        /// <returns>更新是否成功</returns>
        Task<bool> UpdatePlayerScoreAsync(string playerId, int score);

        /// <summary>
        /// 添加房间事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="playerId">相关玩家ID</param>
        /// <param name="description">事件描述</param>
        /// <param name="eventData">事件数据</param>
        /// <returns>是否成功添加</returns>
        Task<bool> AddRoomEventAsync(RoomEventType eventType, string? playerId, string description, Dictionary<string, object>? eventData = null);

        /// <summary>
        /// 获取房间最近事件
        /// </summary>
        /// <param name="count">获取数量</param>
        /// <returns>事件列表</returns>
        Task<List<RoomEvent>> GetRecentEventsAsync(int count = 50);

        /// <summary>
        /// 关闭房间
        /// </summary>
        /// <param name="operatorId">操作者ID</param>
        /// <param name="reason">关闭原因</param>
        /// <returns>是否成功</returns>
        Task<bool> CloseRoomAsync(string operatorId, string? reason = null);

        /// <summary>
        /// 检查房间是否可以开始游戏
        /// </summary>
        /// <returns>是否可以开始</returns>
        Task<bool> CanStartGameAsync();

        /// <summary>
        /// 检查玩家是否有权限操作房间
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="operation">操作类型</param>
        /// <returns>是否有权限</returns>
        Task<bool> HasPermissionAsync(string playerId, RoomOperation operation);

        /// <summary>
        /// 获取房间摘要信息
        /// </summary>
        /// <returns>房间摘要</returns>
        Task<RoomBrief?> GetRoomBriefAsync();
    }

    /// <summary>
    /// 房间操作类型枚举
    /// 用于权限检查
    /// </summary>
    public enum RoomOperation
    {
        UpdateSettings = 0,     // 更新设置
        StartGame = 1,          // 开始游戏
        EndGame = 2,            // 结束游戏
        KickPlayer = 3,         // 踢出玩家
        CloseRoom = 4           // 关闭房间
    }
}