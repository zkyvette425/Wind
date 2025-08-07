using Microsoft.EntityFrameworkCore;
using Wind.Domain.Entities;

namespace Wind.Infrastructure.Persistence
{
    /// <summary>
    /// 游戏数据库上下文
    /// </summary>
    public class GameDbContext : DbContext
    {
        /// <summary>
        /// 玩家集合
        /// </summary>
        public DbSet<Player> Players { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">数据库上下文选项</param>
        public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
        {}

        /// <summary>
        /// 配置实体关系
        /// </summary>
        /// <param name="modelBuilder">模型构建器</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置Player实体
            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(e => e.PlayerId);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Level).HasDefaultValue(1);
                entity.Property(e => e.Experience).HasDefaultValue(0);
                entity.Property(e => e.Gold).HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.LastLoginAt).HasDefaultValueSql("datetime('now')");
            });
        }
    }
}