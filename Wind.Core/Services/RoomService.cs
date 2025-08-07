using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Wind.Core.Interfaces;
using Wind.Core.Models;
using Wind.Shared.Protocols;

namespace Wind.Core.Services
{
    /// <summary>
    /// 房间服务实现
    /// </summary>
    public class RoomService : IRoomService
    {
        private readonly ILogger<RoomService> _logger;
        private readonly ConcurrentDictionary<string, Room> _rooms;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public RoomService(ILogger<RoomService> logger)
        {
            _logger = logger;
            _rooms = new ConcurrentDictionary<string, Room>();
        }

        /// <summary>
        /// 创建房间
        /// </summary>
        public async Task<string> CreateRoomAsync(string roomName, int maxPlayers)
        {
            try
            {
                if (string.IsNullOrEmpty(roomName))
                {
                    _logger.LogWarning("Room name cannot be null or empty");
                    return null;
                }

                if (maxPlayers <= 0)
                {
                    _logger.LogWarning("Max players must be greater than 0");
                    return null;
                }

                var roomId = Guid.NewGuid().ToString();
                var room = new Room(roomId, roomName, maxPlayers);

                if (_rooms.TryAdd(roomId, room))
                {
                    _logger.LogInformation("Room created: {RoomId}, Name: {RoomName}, MaxPlayers: {MaxPlayers}", roomId, roomName, maxPlayers);
                    return roomId;
                }

                _logger.LogError("Failed to create room: {RoomName}", roomName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room: {RoomName}", roomName);
                return null;
            }
        }

        /// <summary>
        /// 加入房间
        /// </summary>
        public async Task<bool> JoinRoomAsync(string roomId, string playerId, string playerName)
        {
            try
            {
                if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(playerName))
                {
                    _logger.LogWarning("RoomId, PlayerId or PlayerName cannot be null or empty");
                    return false;
                }

                if (!_rooms.TryGetValue(roomId, out var room))
                {
                    _logger.LogWarning("Room not found: {RoomId}", roomId);
                    return false;
                }

                if (room.IsFull())
                {
                    _logger.LogWarning("Room is full: {RoomId}", roomId);
                    return false;
                }

                if (room.Players.ContainsKey(playerId))
                {
                    _logger.LogWarning("Player already in room: {PlayerId}, Room: {RoomId}", playerId, roomId);
                    return true; // 已经在房间中，视为成功
                }

                // 创建玩家角色
                var playerCharacter = new PlayerCharacter
                {
                    PlayerId = Guid.Parse(playerId),
                    Name = playerName,
                    Type = "Player",
                    X = 0,
                    Y = 0,
                    Z = 0
                };

                if (room.AddPlayer(playerCharacter))
                {
                    _logger.LogInformation("Player joined room: {PlayerId}, Room: {RoomId}", playerId, roomId);
                    return true;
                }

                _logger.LogError("Failed to add player to room: {PlayerId}, Room: {RoomId}", playerId, roomId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining room: {RoomId}, Player: {PlayerId}", roomId, playerId);
                return false;
            }
        }

        /// <summary>
        /// 离开房间
        /// </summary>
        public async Task<bool> LeaveRoomAsync(string roomId, string playerId)
        {
            try
            {
                if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(playerId))
                {
                    _logger.LogWarning("RoomId or PlayerId cannot be null or empty");
                    return false;
                }

                if (!_rooms.TryGetValue(roomId, out var room))
                {
                    _logger.LogWarning("Room not found: {RoomId}", roomId);
                    return false;
                }

                if (room.RemovePlayer(playerId))
                {
                    _logger.LogInformation("Player left room: {PlayerId}, Room: {RoomId}", playerId, roomId);

                    // 如果房间为空，删除房间
                    if (room.Players.Count == 0)
                    {
                        _rooms.TryRemove(roomId, out _);
                        _logger.LogInformation("Room removed because it's empty: {RoomId}", roomId);
                    }

                    return true;
                }

                _logger.LogWarning("Player not found in room: {PlayerId}, Room: {RoomId}", playerId, roomId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving room: {RoomId}, Player: {PlayerId}", roomId, playerId);
                return false;
            }
        }

        /// <summary>
        /// 获取房间内所有玩家
        /// </summary>
        public async Task<List<PlayerCharacter>> GetPlayersInRoomAsync(string roomId)
        {
            try
            {
                if (string.IsNullOrEmpty(roomId))
                {
                    _logger.LogWarning("RoomId cannot be null or empty");
                    return new List<PlayerCharacter>();
                }

                if (!_rooms.TryGetValue(roomId, out var room))
                {
                    _logger.LogWarning("Room not found: {RoomId}", roomId);
                    return new List<PlayerCharacter>();
                }

                return room.GetAllPlayers();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting players in room: {RoomId}", roomId);
                return new List<PlayerCharacter>();
            }
        }

        /// <summary>
        /// 广播消息到房间内所有玩家
        /// </summary>
        public async Task<bool> BroadcastMessageToRoomAsync(string roomId, ChatMessage message, string senderId)
        {
            try
            {
                if (string.IsNullOrEmpty(roomId) || message == null || string.IsNullOrEmpty(senderId))
                {
                    _logger.LogWarning("RoomId, Message or SenderId cannot be null or empty");
                    return false;
                }

                if (!_rooms.TryGetValue(roomId, out var room))
                {
                    _logger.LogWarning("Room not found: {RoomId}", roomId);
                    return false;
                }

                // 在实际实现中，这里应该通过网络将消息发送给房间内的所有玩家
                // 这里仅作日志记录
                _logger.LogInformation("Message broadcast to room {RoomId} from {SenderId}: {Content}",
                    roomId, senderId, message.Content);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message to room: {RoomId}", roomId);
                return false;
            }
        }

        /// <summary>
        /// 获取所有房间列表
        /// </summary>
        public async Task<List<RoomInfo>> GetAllRoomsAsync()
        {
            try
            {
                return _rooms.Values.Select(room => new RoomInfo
                {
                    RoomId = room.RoomId,
                    RoomName = room.RoomName,
                    CurrentPlayers = room.Players.Count,
                    MaxPlayers = room.MaxPlayers
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all rooms");
                return new List<RoomInfo>();
            }
        }
    }
}