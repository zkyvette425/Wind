using System;
using System.ComponentModel.DataAnnotations;

namespace Wind.Core.Models
{
    /// <summary>
    /// 玩家数据模型
    /// </summary>
    public class PlayerData
    {
        /// <summary>
        /// 玩家唯一ID
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