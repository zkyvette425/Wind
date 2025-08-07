using System.Threading.Tasks;
using Wind.Core.Models;

namespace Wind.Core.Interfaces
{
    /// <summary>
    /// 玩家数据服务接口
    /// </summary>
    public interface IPlayerDataService
    {
        /// <summary>
        /// 根据玩家ID获取玩家数据
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>玩家数据</returns>
        Task<PlayerData> GetPlayerDataByIdAsync(string playerId);

        /// <summary>
        /// 根据用户名获取玩家数据
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>玩家数据</returns>
        Task<PlayerData> GetPlayerDataByUsernameAsync(string username);

        /// <summary>
        /// 创建新玩家数据
        /// </summary>
        /// <param name="playerData">玩家数据</param>
        /// <returns>创建是否成功</returns>
        Task<bool> CreatePlayerDataAsync(PlayerData playerData);

        /// <summary>
        /// 更新玩家数据
        /// </summary>
        /// <param name="playerData">玩家数据</param>
        /// <returns>更新是否成功</returns>
        Task<bool> UpdatePlayerDataAsync(PlayerData playerData);

        /// <summary>
        /// 验证玩家登录信息
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="passwordHash">密码哈希</param>
        /// <returns>验证是否通过</returns>
        Task<bool> ValidatePlayerCredentialsAsync(string username, string passwordHash);
    }
}