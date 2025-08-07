using System;

namespace Wind.Core.Models
{
    /// <summary>
    /// 游戏对象基类
    /// </summary>
    public class GameObject
    {
        /// <summary>
        /// 对象唯一ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 对象类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// X坐标
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        public float Y { get; set; }

        /// <summary>
        /// Z坐标
        /// </summary>
        public float Z { get; set; }

        /// <summary>
        /// 旋转角度
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// 缩放比例
        /// </summary>
        public float Scale { get; set; } = 1.0f;

        /// <summary>
        /// 构造函数
        /// </summary>
        public GameObject()
        {
            Id = Guid.NewGuid();
        }
    }
}