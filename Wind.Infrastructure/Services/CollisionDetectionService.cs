using System.Collections.Generic;
using System.Linq;
using Wind.Domain.Entities;
using Wind.Domain.Services;

namespace Wind.Infrastructure.Services
{
    /// <summary>
    /// 碰撞检测服务实现
    /// </summary>
    public class CollisionDetectionService : ICollisionDetectionService
    {
        private float _collisionPrecision = 0.5f;

        /// <summary>
        /// 检查两个游戏对象是否碰撞
        /// </summary>
        /// <param name="object1">第一个游戏对象</param>
        /// <param name="object2">第二个游戏对象</param>
        /// <returns>是否碰撞</returns>
        public bool CheckCollision(GameObject object1, GameObject object2)
        {
            // 简单的基于距离的碰撞检测
            float distance = CalculateDistance(object1, object2);
            return distance < (_collisionPrecision * (object1.Scale + object2.Scale) / 2);
        }

        /// <summary>
        /// 检查一个游戏对象与多个其他游戏对象是否碰撞
        /// </summary>
        /// <param name="sourceObject">源游戏对象</param>
        /// <param name="otherObjects">其他游戏对象列表</param>
        /// <returns>碰撞的游戏对象列表</returns>
        public IEnumerable<GameObject> CheckCollisions(GameObject sourceObject, IEnumerable<GameObject> otherObjects)
        {
            return otherObjects.Where(obj => CheckCollision(sourceObject, obj)).ToList();
        }

        /// <summary>
        /// 设置碰撞检测精度
        /// </summary>
        /// <param name="precision">精度值</param>
        public void SetCollisionPrecision(float precision)
        {
            if (precision > 0)
            {
                _collisionPrecision = precision;
            }
        }

        /// <summary>
        /// 计算两个游戏对象之间的距离
        /// </summary>
        /// <param name="object1">第一个游戏对象</param>
        /// <param name="object2">第二个游戏对象</param>
        /// <returns>距离</returns>
        private float CalculateDistance(GameObject object1, GameObject object2)
        {
            float dx = object1.X - object2.X;
            float dy = object1.Y - object2.Y;
            float dz = object1.Z - object2.Z;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}