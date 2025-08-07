using Microsoft.Extensions.DependencyInjection;
using Wind.Application.Services;

namespace Wind.Application
{
    /// <summary>
    /// 依赖注入配置
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// 注册应用服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<PlayerService>();
            services.AddScoped<RoomService>();

            return services;
        }
    }
}