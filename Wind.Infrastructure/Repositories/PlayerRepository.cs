using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wind.Domain.Entities;
using Wind.Domain.Repositories;
using Wind.Infrastructure.Persistence;

namespace Wind.Infrastructure.Repositories
{
    /// <summary>
    /// 玩家仓储实现
    /// </summary>
    public class PlayerRepository : IPlayerRepository
    {
        private readonly GameDbContext _context;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="context">数据库上下文</param>
        public PlayerRepository(GameDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 根据ID获取玩家
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>玩家实体</returns>
        public async Task<Player> GetByIdAsync(Guid playerId)
        {
            return await _context.Players.FindAsync(playerId);
        }

        /// <summary>
        /// 根据用户名获取玩家
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>玩家实体</returns>
        public async Task<Player> GetByUsernameAsync(string username)
        {
            return await _context.Players.FirstOrDefaultAsync(p => p.Username == username);
        }

        /// <summary>
        /// 创建新玩家
        /// </summary>
        /// <param name="player">玩家实体</param>
        /// <returns>创建后的玩家实体</returns>
        public async Task<Player> CreateAsync(Player player)
        {
            _context.Players.Add(player);
            await _context.SaveChangesAsync();
            return player;
        }

        /// <summary>
        /// 更新玩家信息
        /// </summary>
        /// <param name="player">玩家实体</param>
        /// <returns>是否更新成功</returns>
        public async Task<bool> UpdateAsync(Player player)
        {
            _context.Entry(player).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await PlayerExistsAsync(player.PlayerId))
                {
                    return false;
                }
                throw;
            }
        }

        /// <summary>
        /// 验证玩家凭据
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="passwordHash">密码哈希</param>
        /// <returns>验证结果</returns>
        public async Task<bool> ValidateCredentialsAsync(string username, string passwordHash)
        {
            var player = await GetByUsernameAsync(username);
            return player != null && player.PasswordHash == passwordHash;
        }

        /// <summary>
        /// 检查玩家是否存在
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>是否存在</returns>
        private async Task<bool> PlayerExistsAsync(Guid playerId)
        {
            return await _context.Players.AnyAsync(e => e.PlayerId == playerId);
        }
    }
}