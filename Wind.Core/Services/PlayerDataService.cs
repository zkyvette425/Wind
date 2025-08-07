using System;using System.Threading.Tasks;
using Wind.Core.Interfaces;
using Wind.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wind.Core.Services
{
    /// <summary>
    /// 玩家数据服务实现
    /// </summary>
    public class PlayerDataService : IPlayerDataService
    {
        private readonly GameDbContext _dbContext;
        private readonly ILogger<PlayerDataService> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dbContext">数据库上下文</param>
        /// <param name="logger">日志记录器</param>
        public PlayerDataService(GameDbContext dbContext, ILogger<PlayerDataService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// 根据玩家ID获取玩家数据
        /// </summary>
        public async Task<PlayerData> GetPlayerDataByIdAsync(string playerId)
        {
            try
            {
                if (!Guid.TryParse(playerId, out var id))
                {
                    _logger.LogWarning("Invalid playerId format: {PlayerId}", playerId);
                    return null;
                }

                return await _dbContext.Players.FirstOrDefaultAsync(p => p.PlayerId == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player data by ID: {PlayerId}", playerId);
                throw;
            }
        }

        /// <summary>
        /// 根据用户名获取玩家数据
        /// </summary>
        public async Task<PlayerData> GetPlayerDataByUsernameAsync(string username)
        {
            try
            {
                return await _dbContext.Players.FirstOrDefaultAsync(p => p.Username == username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player data by username: {Username}", username);
                throw;
            }
        }

        /// <summary>
        /// 创建新玩家数据
        /// </summary>
        public async Task<bool> CreatePlayerDataAsync(PlayerData playerData)
        {
            try
            {
                if (playerData == null)
                {
                    _logger.LogWarning("PlayerData is null");
                    return false;
                }

                if (await GetPlayerDataByUsernameAsync(playerData.Username) != null)
                {
                    _logger.LogWarning("Username already exists: {Username}", playerData.Username);
                    return false;
                }

                playerData.PlayerId = Guid.NewGuid();
                playerData.CreatedAt = DateTime.UtcNow;
                playerData.LastLoginAt = DateTime.UtcNow;

                _dbContext.Players.Add(playerData);
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating player data");
                throw;
            }
        }

        /// <summary>
        /// 更新玩家数据
        /// </summary>
        public async Task<bool> UpdatePlayerDataAsync(PlayerData playerData)
        {
            try
            {
                if (playerData == null)
                {
                    _logger.LogWarning("PlayerData is null");
                    return false;
                }

                var existingPlayer = await GetPlayerDataByIdAsync(playerData.PlayerId.ToString());
                if (existingPlayer == null)
                {
                    _logger.LogWarning("Player not found: {PlayerId}", playerData.PlayerId);
                    return false;
                }

                existingPlayer.Level = playerData.Level;
                existingPlayer.Experience = playerData.Experience;
                existingPlayer.Gold = playerData.Gold;
                existingPlayer.LastLoginAt = DateTime.UtcNow;

                _dbContext.Players.Update(existingPlayer);
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player data: {PlayerId}", playerData.PlayerId);
                throw;
            }
        }

        /// <summary>
        /// 验证玩家登录信息
        /// </summary>
        public async Task<bool> ValidatePlayerCredentialsAsync(string username, string passwordHash)
        {
            try
            {
                // 注意：实际项目中应该存储密码哈希并进行比较
                // 这里仅作示例，实际实现需要更安全的方式
                var player = await GetPlayerDataByUsernameAsync(username);
                return player != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating player credentials: {Username}", username);
                throw;
            }
        }
    }
}