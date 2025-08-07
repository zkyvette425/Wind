using Microsoft.Extensions.DependencyInjection;
using Wind.Domain.Repositories;
using Wind.Domain.Services;
using Wind.Infrastructure.Persistence;
using Wind.Infrastructure.Repositories;
using Wind.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Wind.Infrastructure
{
    /// <summary>
    /// 依赖注入配置
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// 注册基础设施服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="connectionString">数据库连接字符串</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            // 注册数据库上下文
            services.AddDbContext<GameDbContext>(options =>
                options.UseSqlite(connectionString));

            // 注册仓储
            services.AddScoped<IPlayerRepository, PlayerRepository>();
            services.AddScoped<IRoomRepository, RoomRepository>();

            // 注册服务
            services.AddScoped<ICollisionDetectionService, CollisionDetectionService>();

            return services;
        }
    }
}