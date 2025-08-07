using System.ComponentModel.DataAnnotations;

namespace Wind.Domain.Entities
{
    /// <summary>
    /// 玩家实体
    /// </summary>
    public class Player
    {
        /// <summary>
        /// 玩家ID
        /// </summary>
        [Key]
        public Guid PlayerId { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Username { get; set; }

        /// <summary>
        /// 密码哈希
        /// </summary>
        [Required]
        public string PasswordHash { get; set; }

        /// <summary>
        /// 等级
        /// </summary>
        public int Level { get; set; } = 1;

        /// <summary>
        /// 经验值
        /// </summary>
        public int Experience { get; set; } = 0;

        /// <summary>
        /// 金币
        /// </summary>
        public int Gold { get; set; } = 0;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    }
}