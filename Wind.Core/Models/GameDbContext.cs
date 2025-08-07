using Microsoft.EntityFrameworkCore;

namespace Wind.Core.Models
{
    /// <summary>
    /// 游戏数据库上下文
    /// </summary>
    public class GameDbContext : DbContext
    {
        /// <summary>
        /// 玩家数据集合
        /// </summary>
        public DbSet<PlayerData> Players { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">数据库上下文选项</param>
        public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// 配置数据库连接
        /// </summary>
        /// <param name="optionsBuilder">选项构建器</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // 使用SQLite数据库
                optionsBuilder.UseSqlite("Data Source=game.db");
            }
        }
    }
}