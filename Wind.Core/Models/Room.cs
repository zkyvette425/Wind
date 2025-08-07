using System.Collections.Concurrent;

namespace Wind.Core.Models
{
    /// <summary>
    /// 房间类
    /// </summary>
    public class Room
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
        /// 最大玩家数
        /// </summary>
        public int MaxPlayers { get; set; }

        /// <summary>
        /// 房间内的玩家字典
        /// </summary>
        public ConcurrentDictionary<string, PlayerCharacter> Players { get; set; }

        /// <summary>
        /// 房间创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="roomName">房间名称</param>
        /// <param name="maxPlayers">最大玩家数</param>
        public Room(string roomId, string roomName, int maxPlayers)
        {
            RoomId = roomId;
            RoomName = roomName;
            MaxPlayers = maxPlayers;
            Players = new ConcurrentDictionary<string, PlayerCharacter>();
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 添加玩家到房间
        /// </summary>
        /// <param name="player">玩家对象</param>
        /// <returns>是否添加成功</returns>
        public bool AddPlayer(PlayerCharacter player)
        {
            if (Players.Count >= MaxPlayers)
                return false;

            return Players.TryAdd(player.PlayerId.ToString(), player);
        }

        /// <summary>
        /// 从房间移除玩家
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>是否移除成功</returns>
        public bool RemovePlayer(string playerId)
        {
            return Players.TryRemove(playerId, out _);
        }

        /// <summary>
        /// 获取房间内的所有玩家
        /// </summary>
        /// <returns>玩家列表</returns>
        public List<PlayerCharacter> GetAllPlayers()
        {
            return new List<PlayerCharacter>(Players.Values);
        }

        /// <summary>
        /// 检查房间是否已满
        /// </summary>
        /// <returns>是否已满</returns>
        public bool IsFull()
        {
            return Players.Count >= MaxPlayers;
        }
    }
}