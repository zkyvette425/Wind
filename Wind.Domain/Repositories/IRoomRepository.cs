namespace Wind.Domain.Repositories
{
    /// <summary>
    /// 房间仓储接口
    /// </summary>
    public interface IRoomRepository
    {
        /// <summary>
        /// 创建房间
        /// </summary>
        /// <param name="room">房间实体</param>
        /// <returns>创建后的房间实体</returns>
        Task<Entities.Room> CreateAsync(Entities.Room room);

        /// <summary>
        /// 根据ID获取房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <returns>房间实体</returns>
        Task<Entities.Room> GetByIdAsync(Guid roomId);

        /// <summary>
        /// 获取所有房间
        /// </summary>
        /// <returns>房间列表</returns>
        Task<IEnumerable<Entities.Room>> GetAllAsync();

        /// <summary>
        /// 更新房间信息
        /// </summary>
        /// <param name="room">房间实体</param>
        /// <returns>是否更新成功</returns>
        Task<bool> UpdateAsync(Entities.Room room);

        /// <summary>
        /// 删除房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <returns>是否删除成功</returns>
        Task<bool> DeleteAsync(Guid roomId);
    }
}