using System;
using System.Threading.Tasks;
using Wind.Domain.Entities;
using Wind.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Wind.Application.Services
{
    /// <summary>
    /// 玩家服务
    /// </summary>
    public class PlayerService
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly ILogger<PlayerService> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="playerRepository">玩家仓储</param>
        /// <param name="logger">日志记录器</param>
        public PlayerService(IPlayerRepository playerRepository, ILogger<PlayerService> logger)
        {
            _playerRepository = playerRepository;
            _logger = logger;
        }

        /// <summary>
        /// 根据ID获取玩家数据
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>玩家实体</returns>
        public async Task<Player> GetPlayerDataByIdAsync(Guid playerId)
        {
            try
            {
                _logger.LogInformation("Getting player data for player with ID: {PlayerId}", playerId);
                return await _playerRepository.GetByIdAsync(playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player data for player with ID: {PlayerId}", playerId);
                throw;
            }
        }

        /// <summary>
        /// 根据用户名获取玩家数据
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>玩家实体</returns>
        public async Task<Player> GetPlayerDataByUsernameAsync(string username)
        {
            try
            {
                _logger.LogInformation("Getting player data for username: {Username}", username);
                return await _playerRepository.GetByUsernameAsync(username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player data for username: {Username}", username);
                throw;
            }
        }

        /// <summary>
        /// 创建新玩家
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="passwordHash">密码哈希</param>
        /// <returns>创建的玩家实体</returns>
        public async Task<Player> CreatePlayerDataAsync(string username, string passwordHash)
        {
            try
            {
                _logger.LogInformation("Creating new player with username: {Username}", username);

                // 检查用户名是否已存在
                var existingPlayer = await _playerRepository.GetByUsernameAsync(username);
                if (existingPlayer != null)
                {
                    _logger.LogWarning("Username {Username} is already taken", username);
                    throw new ArgumentException("Username is already taken");
                }

                // 创建新玩家
                var newPlayer = new Player
                {
                    PlayerId = Guid.NewGuid(),
                    Username = username,
                    PasswordHash = passwordHash,
                    Level = 1,
                    Experience = 0,
                    Gold = 0,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                return await _playerRepository.CreateAsync(newPlayer);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating player with username: {Username}", username);
                throw;
            }
        }

        /// <summary>
        /// 更新玩家数据
        /// </summary>
        /// <param name="player">玩家实体</param>
        /// <returns>是否更新成功</returns>
        public async Task<bool> UpdatePlayerDataAsync(Player player)
        {
            try
            {
                _logger.LogInformation("Updating player data for player with ID: {PlayerId}", player.PlayerId);
                player.LastLoginAt = DateTime.UtcNow;
                return await _playerRepository.UpdateAsync(player);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player data for player with ID: {PlayerId}", player.PlayerId);
                throw;
            }
        }

        /// <summary>
        /// 验证玩家凭据
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="passwordHash">密码哈希</param>
        /// <returns>验证结果</returns>
        public async Task<bool> ValidatePlayerCredentialsAsync(string username, string passwordHash)
        {
            try
            {
                _logger.LogInformation("Validating credentials for username: {Username}", username);
                return await _playerRepository.ValidateCredentialsAsync(username, passwordHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating credentials for username: {Username}", username);
                throw;
            }
        }
    }
}