namespace Wind.Domain.Entities
{
    /// <summary>
    /// 玩家角色实体
    /// </summary>
    public class PlayerCharacter : GameObject
    {
        /// <summary>
        /// 所属玩家ID
        /// </summary>
        public Guid PlayerId { get; private set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 等级
        /// </summary>
        public int Level { get; private set; } = 1;

        /// <summary>
        /// 生命值
        /// </summary>
        public int Health { get; private set; } = 100;

        /// <summary>
        /// 最大生命值
        /// </summary>
        public int MaxHealth { get; private set; } = 100;

        /// <summary>
        /// 法力值
        /// </summary>
        public int Mana { get; private set; } = 50;

        /// <summary>
        /// 最大法力值
        /// </summary>
        public int MaxMana { get; private set; } = 50;

        /// <summary>
        /// 攻击力
        /// </summary>
        public int Attack { get; private set; } = 10;

        /// <summary>
        /// 防御力
        /// </summary>
        public int Defense { get; private set; } = 5;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="name">角色名称</param>
        public PlayerCharacter(Guid playerId, string name)
        {
            PlayerId = playerId;
            Name = name;
            Type = "PlayerCharacter";
        }

        /// <summary>
        /// 升级
        /// </summary>
        public void LevelUp()
        {
            Level++;
            MaxHealth += 20;
            MaxMana += 10;
            Attack += 3;
            Defense += 2;
            Health = MaxHealth;
            Mana = MaxMana;
        }

        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        public void TakeDamage(int damage)
        {
            int actualDamage = Math.Max(0, damage - Defense);
            Health = Math.Max(0, Health - actualDamage);
        }

        /// <summary>
        /// 治疗
        /// </summary>
        /// <param name="healAmount">治疗量</param>
        public void Heal(int healAmount)
        {
            Health = Math.Min(MaxHealth, Health + healAmount);
        }

        /// <summary>
        /// 使用法力
        /// </summary>
        /// <param name="manaCost">法力消耗</param>
        /// <returns>是否成功使用</returns>
        public bool UseMana(int manaCost)
        {
            if (Mana >= manaCost)
            {
                Mana -= manaCost;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 恢复法力
        /// </summary>
        /// <param name="manaAmount">法力恢复量</param>
        public void RestoreMana(int manaAmount)
        {
            Mana = Math.Min(MaxMana, Mana + manaAmount);
        }
    }
}