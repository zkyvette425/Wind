using Wind.Core.Models;
using Wind.Shared.Protocols;

namespace Wind.Core.Interfaces
{
    /// <summary>
    /// 房间服务接口
    /// </summary>
    public interface IRoomService
    {
        /// <summary>
        /// 创建房间
        /// </summary>
        /// <param name="roomName">房间名称</param>
        /// <param name="maxPlayers">最大玩家数</param>
        /// <returns>房间ID</returns>
        Task<string> CreateRoomAsync(string roomName, int maxPlayers);

        /// <summary>
        /// 加入房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="playerName">玩家名称</param>
        /// <returns>是否加入成功</returns>
        Task<bool> JoinRoomAsync(string roomId, string playerId, string playerName);

        /// <summary>
        /// 离开房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <returns>是否离开成功</returns>
        Task<bool> LeaveRoomAsync(string roomId, string playerId);

        /// <summary>
        /// 获取房间内所有玩家
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <returns>玩家列表</returns>
        Task<List<PlayerCharacter>> GetPlayersInRoomAsync(string roomId);

        /// <summary>
        /// 广播消息到房间内所有玩家
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="message">消息内容</param>
        /// <param name="senderId">发送者ID</param>
        /// <returns>是否广播成功</returns>
        Task<bool> BroadcastMessageToRoomAsync(string roomId, ChatMessage message, string senderId);

        /// <summary>
        /// 获取所有房间列表
        /// </summary>
        /// <returns>房间列表</returns>
        Task<List<RoomInfo>> GetAllRoomsAsync();
    }

    /// <summary>
    /// 房间信息类
    /// </summary>
    public class RoomInfo
    {
        /// <summary>
        /// 房间ID
        /// </summary>
        public string RoomId { get; set; }

        /// <summary>
        /// 房间名称
        /// </summary>
        public string RoomName { get; set; }

        /// <summary>
        /// 当前玩家数
        /// </summary>
        public int CurrentPlayers { get; set; }

        /// <summary>
        /// 最大玩家数
        /// </summary>
        public int MaxPlayers { get; set; }
    }
}