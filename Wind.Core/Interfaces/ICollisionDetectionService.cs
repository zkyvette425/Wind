using System.Collections.Generic;
using Wind.Core.Models;

namespace Wind.Core.Interfaces
{
    /// <summary>
    /// 碰撞检测服务接口
    /// </summary>
    public interface ICollisionDetectionService
    {
        /// <summary>
        /// 检查两个游戏对象是否发生碰撞
        /// </summary>
        /// <param name="object1">第一个游戏对象</param>
        /// <param name="object2">第二个游戏对象</param>
        /// <returns>是否发生碰撞</returns>
        bool CheckCollision(GameObject object1, GameObject object2);

        /// <summary>
        /// 检查对象与多个其他对象是否发生碰撞
        /// </summary>
        /// <param name="sourceObject">源游戏对象</param>
        /// <param name="otherObjects">其他游戏对象列表</param>
        /// <returns>发生碰撞的对象列表</returns>
        List<GameObject> CheckCollisions(GameObject sourceObject, List<GameObject> otherObjects);

        /// <summary>
        /// 设置碰撞检测精度
        /// </summary>
        /// <param name="precision">精度值</param>
        void SetCollisionPrecision(float precision);
    }
}