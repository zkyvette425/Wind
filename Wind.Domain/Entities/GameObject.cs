namespace Wind.Domain.Entities
{
    /// <summary>
    /// 游戏对象实体基类
    /// </summary>
    public abstract class GameObject
    {
        /// <summary>
        /// 对象ID
        /// </summary>
        public Guid Id { get; protected set; }

        /// <summary>
        /// 对象类型
        /// </summary>
        public string Type { get; protected set; }

        /// <summary>
        /// X坐标
        /// </summary>
        public float X { get; protected set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        public float Y { get; protected set; }

        /// <summary>
        /// Z坐标
        /// </summary>
        public float Z { get; protected set; }

        /// <summary>
        /// 旋转角度
        /// </summary>
        public float Rotation { get; protected set; }

        /// <summary>
        /// 缩放比例
        /// </summary>
        public float Scale { get; protected set; } = 1.0f;

        /// <summary>
        /// 构造函数
        /// </summary>
        protected GameObject()
        {
            Id = Guid.NewGuid();
        }

        /// <summary>
        /// 更新位置
        /// </summary>
        /// <param name="x">新X坐标</param>
        /// <param name="y">新Y坐标</param>
        /// <param name="z">新Z坐标</param>
        public virtual void UpdatePosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 更新旋转
        /// </summary>
        /// <param name="rotation">新旋转角度</param>
        public virtual void UpdateRotation(float rotation)
        {
            Rotation = rotation;
        }

        /// <summary>
        /// 更新缩放
        /// </summary>
        /// <param name="scale">新缩放比例</param>
        public virtual void UpdateScale(float scale)
        {
            if (scale > 0)
            {
                Scale = scale;
            }
        }
    }
}