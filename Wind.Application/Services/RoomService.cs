using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wind.Domain.Entities;
using Wind.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Wind.Application.Services
{
    /// <summary>
    /// 房间服务
    /// </summary>
    public class RoomService
    {
        private readonly IRoomRepository _roomRepository;
        private readonly ILogger<RoomService> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="roomRepository">房间仓储</param>
        /// <param name="logger">日志记录器</param>
        public RoomService(IRoomRepository roomRepository, ILogger<RoomService> logger)
        {
            _roomRepository = roomRepository;
            _logger = logger;
        }

        /// <summary>
        /// 创建房间
        /// </summary>
        /// <param name="roomName">房间名称</param>
        /// <param name="maxPlayers">最大玩家数</param>
        /// <returns>创建的房间</returns>
        public async Task<Room> CreateRoomAsync(string roomName, int maxPlayers)
        {
            try
            {
                _logger.LogInformation("Creating room: {RoomName} with max players: {MaxPlayers}", roomName, maxPlayers);
                var room = new Room(roomName, maxPlayers);
                return await _roomRepository.CreateAsync(room);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room: {RoomName}", roomName);
                throw;
            }
        }

        /// <summary>
        /// 加入房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerCharacter">玩家角色</param>
        /// <returns>是否加入成功</returns>
        public async Task<bool> JoinRoomAsync(Guid roomId, PlayerCharacter playerCharacter)
        {
            try
            {
                _logger.LogInformation("Player {PlayerId} joining room {RoomId}", playerCharacter.PlayerId, roomId);
                var room = await _roomRepository.GetByIdAsync(roomId);
                if (room == null)
                {
                    _logger.LogWarning("Room {RoomId} not found", roomId);
                    return false;
                }

                if (room.IsFull())
                {
                    _logger.LogWarning("Room {RoomId} is full", roomId);
                    return false;
                }

                var result = room.AddPlayer(playerCharacter);
                if (result)
                {
                    await _roomRepository.UpdateAsync(room);
                    _logger.LogInformation("Player {PlayerId} joined room {RoomId} successfully", playerCharacter.PlayerId, roomId);
                }
                else
                {
                    _logger.LogWarning("Failed to add player {PlayerId} to room {RoomId}", playerCharacter.PlayerId, roomId);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining room {RoomId} for player {PlayerId}", roomId, playerCharacter.PlayerId);
                throw;
            }
        }

        /// <summary>
        /// 离开房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerCharacterId">玩家角色ID</param>
        /// <returns>是否离开成功</returns>
        public async Task<bool> LeaveRoomAsync(Guid roomId, Guid playerCharacterId)
        {
            try
            {
                _logger.LogInformation("Player character {PlayerCharacterId} leaving room {RoomId}", playerCharacterId, roomId);
                var room = await _roomRepository.GetByIdAsync(roomId);
                if (room == null)
                {
                    _logger.LogWarning("Room {RoomId} not found", roomId);
                    return false;
                }

                var result = room.RemovePlayer(playerCharacterId);
                if (result)
                {
                    await _roomRepository.UpdateAsync(room);
                    _logger.LogInformation("Player character {PlayerCharacterId} left room {RoomId} successfully", playerCharacterId, roomId);

                    // 如果房间为空，删除房间
                    if (room.CurrentPlayerCount == 0)
                    {
                        await _roomRepository.DeleteAsync(roomId);
                        _logger.LogInformation("Room {RoomId} is empty, deleted successfully", roomId);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to remove player character {PlayerCharacterId} from room {RoomId}", playerCharacterId, roomId);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving room {RoomId} for player character {PlayerCharacterId}", roomId, playerCharacterId);
                throw;
            }
        }

        /// <summary>
        /// 获取房间中的所有玩家
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <returns>玩家角色列表</returns>
        public async Task<IEnumerable<PlayerCharacter>> GetRoomPlayersAsync(Guid roomId)
        {
            try
            {
                _logger.LogInformation("Getting players for room {RoomId}", roomId);
                var room = await _roomRepository.GetByIdAsync(roomId);
                return room?.GetAllPlayers() ?? Enumerable.Empty<PlayerCharacter>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting players for room {RoomId}", roomId);
                throw;
            }
        }

        /// <summary>
        /// 获取所有房间
        /// </summary>
        /// <returns>房间列表</returns>
        public async Task<IEnumerable<Room>> GetAllRoomsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all rooms");
                return await _roomRepository.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all rooms");
                throw;
            }
        }
    }
}