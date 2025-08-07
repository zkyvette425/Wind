using Microsoft.Extensions.Logging;
using Wind.Core.Interfaces;
using Wind.Core.Models;

namespace Wind.Core.Services
{
    /// <summary>
    /// 碰撞检测服务实现
    /// </summary>
    public class CollisionDetectionService : ICollisionDetectionService
    {
        private readonly ILogger<CollisionDetectionService> _logger;
        private float _collisionPrecision = 0.5f;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public CollisionDetectionService(ILogger<CollisionDetectionService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 检查两个游戏对象是否发生碰撞
        /// </summary>
        public bool CheckCollision(GameObject object1, GameObject object2)
        {
            if (object1 == null || object2 == null)
            {
                _logger.LogWarning("One or both game objects are null");
                return false;
            }

            try
            {
                // 计算两个对象之间的距离
                float distance = (float)Math.Sqrt(
                    Math.Pow(object1.X - object2.X, 2) +
                    Math.Pow(object1.Y - object2.Y, 2) +
                    Math.Pow(object1.Z - object2.Z, 2));

                // 计算碰撞半径之和
                float collisionRadiusSum = (object1.Scale + object2.Scale) * _collisionPrecision;

                // 如果距离小于碰撞半径之和，则发生碰撞
                return distance < collisionRadiusSum;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking collision between objects");
                return false;
            }
        }

        /// <summary>
        /// 检查对象与多个其他对象是否发生碰撞
        /// </summary>
        public List<GameObject> CheckCollisions(GameObject sourceObject, List<GameObject> otherObjects)
        {
            var collidingObjects = new List<GameObject>();

            if (sourceObject == null || otherObjects == null)
            {
                _logger.LogWarning("Source object or other objects list is null");
                return collidingObjects;
            }

            foreach (var obj in otherObjects)
            {
                if (obj == sourceObject) continue;

                if (CheckCollision(sourceObject, obj))
                {
                    collidingObjects.Add(obj);
                }
            }

            return collidingObjects;
        }

        /// <summary>
        /// 设置碰撞检测精度
        /// </summary>
        public void SetCollisionPrecision(float precision)
        {
            if (precision <= 0 || precision > 1)
            {
                _logger.LogWarning("Invalid collision precision: {Precision}. Must be between 0 and 1.", precision);
                return;
            }

            _collisionPrecision = precision;
            _logger.LogInformation("Collision precision set to: {Precision}", precision);
        }
    }
}