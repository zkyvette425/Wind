namespace Wind.Domain.Entities
{
    /// <summary>
    /// 房间实体
    /// </summary>
    public class Room
    {
        /// <summary>
        /// 房间ID
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// 房间名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 最大玩家数
        /// </summary>
        public int MaxPlayers { get; private set; }

        /// <summary>
        /// 房间中的玩家角色
        /// </summary>
        private readonly Dictionary<Guid, PlayerCharacter> _players = new Dictionary<Guid, PlayerCharacter>();

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">房间名称</param>
        /// <param name="maxPlayers">最大玩家数</param>
        public Room(string name, int maxPlayers)
        {
            Id = Guid.NewGuid();
            Name = name;
            MaxPlayers = maxPlayers > 0 ? maxPlayers : 10;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 添加玩家到房间
        /// </summary>
        /// <param name="playerCharacter">玩家角色</param>
        /// <returns>是否添加成功</returns>
        public bool AddPlayer(PlayerCharacter playerCharacter)
        {
            if (_players.Count >= MaxPlayers || _players.ContainsKey(playerCharacter.Id))
            {
                return false;
            }

            _players.Add(playerCharacter.Id, playerCharacter);
            return true;
        }

        /// <summary>
        /// 从房间移除玩家
        /// </summary>
        /// <param name="playerCharacterId">玩家角色ID</param>
        /// <returns>是否移除成功</returns>
        public bool RemovePlayer(Guid playerCharacterId)
        {
            return _players.Remove(playerCharacterId);
        }

        /// <summary>
        /// 获取房间中的所有玩家
        /// </summary>
        /// <returns>玩家角色列表</returns>
        public IEnumerable<PlayerCharacter> GetAllPlayers()
        {
            return _players.Values.ToList();
        }

        /// <summary>
        /// 检查房间是否已满
        /// </summary>
        /// <returns>是否已满</returns>
        public bool IsFull()
        {
            return _players.Count >= MaxPlayers;
        }

        /// <summary>
        /// 获取当前玩家数量
        /// </summary>
        public int CurrentPlayerCount => _players.Count;
    }
}