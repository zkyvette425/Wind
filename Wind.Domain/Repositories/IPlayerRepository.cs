namespace Wind.Domain.Repositories
{
    /// <summary>
    /// 玩家仓储接口
    /// </summary>
    public interface IPlayerRepository
    {
        /// <summary>
        /// 根据ID获取玩家
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>玩家实体</returns>
        Task<Entities.Player> GetByIdAsync(Guid playerId);

        /// <summary>
        /// 根据用户名获取玩家
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>玩家实体</returns>
        Task<Entities.Player> GetByUsernameAsync(string username);

        /// <summary>
        /// 创建新玩家
        /// </summary>
        /// <param name="player">玩家实体</param>
        /// <returns>创建后的玩家实体</returns>
        Task<Entities.Player> CreateAsync(Entities.Player player);

        /// <summary>
        /// 更新玩家信息
        /// </summary>
        /// <param name="player">玩家实体</param>
        /// <returns>是否更新成功</returns>
        Task<bool> UpdateAsync(Entities.Player player);

        /// <summary>
        /// 验证玩家凭据
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="passwordHash">密码哈希</param>
        /// <returns>验证结果</returns>
        Task<bool> ValidateCredentialsAsync(string username, string passwordHash);
    }
}