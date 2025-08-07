using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wind.Domain.Entities;
using Wind.Domain.Repositories;
using Wind.Infrastructure.Persistence;

namespace Wind.Infrastructure.Repositories
{
    /// <summary>
    /// 房间仓储实现
    /// </summary>
    public class RoomRepository : IRoomRepository
    {
        private readonly GameDbContext _context;
        private readonly Dictionary<Guid, Room> _rooms = new Dictionary<Guid, Room>();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="context">数据库上下文</param>
        public RoomRepository(GameDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 创建房间
        /// </summary>
        /// <param name="room">房间实体</param>
        /// <returns>创建后的房间实体</returns>
        public async Task<Room> CreateAsync(Room room)
        {
            _rooms.Add(room.Id, room);
            return await Task.FromResult(room);
        }

        /// <summary>
        /// 根据ID获取房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <returns>房间实体</returns>
        public async Task<Room> GetByIdAsync(Guid roomId)
        {
            _rooms.TryGetValue(roomId, out var room);
            return await Task.FromResult(room);
        }

        /// <summary>
        /// 获取所有房间
        /// </summary>
        /// <returns>房间列表</returns>
        public async Task<IEnumerable<Room>> GetAllAsync()
        {
            return await Task.FromResult(_rooms.Values.ToList());
        }

        /// <summary>
        /// 更新房间信息
        /// </summary>
        /// <param name="room">房间实体</param>
        /// <returns>是否更新成功</returns>
        public async Task<bool> UpdateAsync(Room room)
        {
            if (!_rooms.ContainsKey(room.Id))
            {
                return false;
            }

            _rooms[room.Id] = room;
            return await Task.FromResult(true);
        }

        /// <summary>
        /// 删除房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <returns>是否删除成功</returns>
        public async Task<bool> DeleteAsync(Guid roomId)
        {
            return await Task.FromResult(_rooms.Remove(roomId));
        }
    }
}