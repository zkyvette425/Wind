using Wind.Domain.Entities;

namespace Wind.Domain.Services
{
    /// <summary>
    /// 碰撞检测服务接口
    /// </summary>
    public interface ICollisionDetectionService
    {
        /// <summary>
        /// 检查两个游戏对象是否碰撞
        /// </summary>
        /// <param name="object1">第一个游戏对象</param>
        /// <param name="object2">第二个游戏对象</param>
        /// <returns>是否碰撞</returns>
        bool CheckCollision(GameObject object1, GameObject object2);

        /// <summary>
        /// 检查一个游戏对象与多个其他游戏对象是否碰撞
        /// </summary>
        /// <param name="sourceObject">源游戏对象</param>
        /// <param name="otherObjects">其他游戏对象列表</param>
        /// <returns>碰撞的游戏对象列表</returns>
        IEnumerable<GameObject> CheckCollisions(GameObject sourceObject, IEnumerable<GameObject> otherObjects);

        /// <summary>
        /// 设置碰撞检测精度
        /// </summary>
        /// <param name="precision">精度值</param>
        void SetCollisionPrecision(float precision);
    }
}