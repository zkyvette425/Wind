using Wind.Shared.Protocols;

namespace Wind.Core.Models
{
    /// <summary>
    /// 玩家角色类
    /// </summary>
    public class PlayerCharacter : GameObject
    {
        /// <summary>
        /// 玩家ID
        /// </summary>
        public Guid PlayerId { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 角色等级
        /// </summary>
        public int Level { get; set; } = 1;

        /// <summary>
        /// 当前生命值
        /// </summary>
        public int CurrentHealth { get; set; } = 100;

        /// <summary>
        /// 最大生命值
        /// </summary>
        public int MaxHealth { get; set; } = 100;

        /// <summary>
        /// 当前魔法值
        /// </summary>
        public int CurrentMana { get; set; } = 50;

        /// <summary>
        /// 最大魔法值
        /// </summary>
        public int MaxMana { get; set; } = 50;

        /// <summary>
        /// 移动速度
        /// </summary>
        public float MoveSpeed { get; set; } = 5.0f;

        /// <summary>
        /// 更新角色位置
        /// </summary>
        /// <param name="positionUpdate">位置更新消息</param>
        public void UpdatePosition(PositionUpdateMessage positionUpdate)
        {
            if (positionUpdate == null)
                return;

            X = positionUpdate.X;
            Y = positionUpdate.Y;
            Z = positionUpdate.Z;
        }

        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        public void TakeDamage(int damage)
        {
            CurrentHealth = Math.Max(0, CurrentHealth - damage);
        }

        /// <summary>
        /// 恢复生命值
        /// </summary>
        /// <param name="heal">治疗值</param>
        public void Heal(int heal)
        {
            CurrentHealth = Math.Min(MaxHealth, CurrentHealth + heal);
        }
    }
}