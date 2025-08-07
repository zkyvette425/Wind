using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wind.Core.Interfaces;
using Wind.Core.Models;
using Wind.Core.Services;

namespace Wind.Tests.IntegrationTests
{
    /// <summary>
    /// 玩家数据服务集成测试
    /// </summary>
    public class PlayerDataServiceIntegrationTests
    {
        private readonly GameDbContext _dbContext;
        private readonly IPlayerDataService _playerDataService;

        /// <summary>
        /// 构造函数
        /// </summary>
        public PlayerDataServiceIntegrationTests()
        {
            // 使用内存数据库进行测试
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;

            _dbContext = new GameDbContext(options);
            var loggerMock = new Mock<ILogger<PlayerDataService>>();

            _playerDataService = new PlayerDataService(_dbContext, loggerMock.Object);

            // 确保数据库已创建
            _dbContext.Database.EnsureCreated();
        }

        /// <summary>
        /// 测试创建玩家数据
        /// </summary>
        [Fact]
        public async Task CreatePlayerDataAsync_ShouldReturnTrue_WhenPlayerIsCreated()
        {
            // Arrange
            var playerData = new PlayerData
            {
                Username = "testuser",
                Level = 1,
                Experience = 0,
                Gold = 100
            };

            // Act
            var result = await _playerDataService.CreatePlayerDataAsync(playerData);

            // Assert
            Assert.True(result);
            var createdPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.Username == "testuser");
            Assert.NotNull(createdPlayer);
            Assert.Equal("testuser", createdPlayer.Username);
        }

        /// <summary>
        /// 测试根据用户名获取玩家数据
        /// </summary>
        [Fact]
        public async Task GetPlayerDataByUsernameAsync_ShouldReturnPlayer_WhenPlayerExists()
        {
            // Arrange
            var playerData = new PlayerData
            {
                Username = "testuser2",
                Level = 1,
                Experience = 0,
                Gold = 100
            };
            await _playerDataService.CreatePlayerDataAsync(playerData);

            // Act
            var result = await _playerDataService.GetPlayerDataByUsernameAsync("testuser2");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("testuser2", result.Username);
        }

        /// <summary>
        /// 测试更新玩家数据
        /// </summary>
        [Fact]
        public async Task UpdatePlayerDataAsync_ShouldReturnTrue_WhenPlayerIsUpdated()
        {
            // Arrange
            var playerData = new PlayerData
            {
                Username = "testuser3",
                Level = 1,
                Experience = 0,
                Gold = 100
            };
            await _playerDataService.CreatePlayerDataAsync(playerData);

            var createdPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.Username == "testuser3");
            createdPlayer.Level = 2;
            createdPlayer.Gold = 200;

            // Act
            var result = await _playerDataService.UpdatePlayerDataAsync(createdPlayer);

            // Assert
            Assert.True(result);
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.Username == "testuser3");
            Assert.Equal(2, updatedPlayer.Level);
            Assert.Equal(200, updatedPlayer.Gold);
        }

        /// <summary>
        /// 测试验证玩家凭据
        /// </summary>
        [Fact]
        public async Task ValidatePlayerCredentialsAsync_ShouldReturnTrue_WhenPlayerExists()
        {
            // Arrange
            var playerData = new PlayerData
            {
                Username = "testuser4",
                Level = 1,
                Experience = 0,
                Gold = 100
            };
            await _playerDataService.CreatePlayerDataAsync(playerData);

            // Act
            var result = await _playerDataService.ValidatePlayerCredentialsAsync("testuser4", "password");

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// 清理测试数据
        /// </summary>
        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }
    }
}