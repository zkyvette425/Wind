using MagicOnion;
using System;
using System.Threading.Tasks;

namespace Wind.Shared.Services
{
    /// <summary>
    /// 玩家StreamingHub接口 - 处理实时双向通信
    /// 支持玩家连接状态管理、实时消息推送、房间内状态同步等功能
    /// </summary>
    public interface IPlayerHub : IStreamingHub<IPlayerHub, IPlayerHubReceiver>
    {
        #region 连接管理
        
        /// <summary>
        /// 玩家上线通知 - 建立Hub连接后调用
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="accessToken">JWT访问令牌</param>
        /// <returns></returns>
        ValueTask OnlineAsync(string playerId, string accessToken);
        
        /// <summary>
        /// 玩家下线通知 - 主动断开连接前调用
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns></returns>
        ValueTask OfflineAsync(string playerId);
        
        /// <summary>
        /// 心跳检测 - 保持连接活跃状态
        /// </summary>
        /// <returns>服务器时间戳</returns>
        ValueTask<long> HeartbeatAsync();
        
        #endregion
        
        #region 房间相关
        
        /// <summary>
        /// 加入房间实时通信群组
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <returns></returns>
        ValueTask JoinRoomAsync(string roomId, string playerId);
        
        /// <summary>
        /// 离开房间实时通信群组
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <returns></returns>
        ValueTask LeaveRoomAsync(string roomId, string playerId);
        
        /// <summary>
        /// 广播玩家在房间内的状态更新
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="newStatus">新状态 (Ready/NotReady/Playing等)</param>
        /// <returns></returns>
        ValueTask UpdatePlayerStatusAsync(string playerId, string newStatus);
        
        /// <summary>
        /// 广播玩家位置信息更新
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="z">Z坐标</param>
        /// <returns></returns>
        ValueTask UpdatePlayerPositionAsync(string playerId, float x, float y, float z);
        
        #endregion
        
        #region 实时消息
        
        /// <summary>
        /// 发送房间聊天消息
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">发送者ID</param>
        /// <param name="message">消息内容</param>
        /// <returns></returns>
        ValueTask SendRoomMessageAsync(string roomId, string playerId, string message);
        
        /// <summary>
        /// 发送私聊消息
        /// </summary>
        /// <param name="fromPlayerId">发送者ID</param>
        /// <param name="toPlayerId">接收者ID</param>
        /// <param name="message">消息内容</param>
        /// <returns></returns>
        ValueTask SendPrivateMessageAsync(string fromPlayerId, string toPlayerId, string message);
        
        /// <summary>
        /// 发送系统通知
        /// </summary>
        /// <param name="playerId">目标玩家ID</param>
        /// <param name="notificationType">通知类型</param>
        /// <param name="content">通知内容</param>
        /// <returns></returns>
        ValueTask SendSystemNotificationAsync(string playerId, string notificationType, string content);
        
        #endregion
        
        #region 匹配相关
        
        /// <summary>
        /// 加入匹配队列通知
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="matchmakingRequest">匹配请求数据</param>
        /// <returns></returns>
        ValueTask JoinMatchmakingAsync(string playerId, string matchmakingRequest);
        
        /// <summary>
        /// 离开匹配队列通知
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns></returns>
        ValueTask LeaveMatchmakingAsync(string playerId);
        
        #endregion
        
        #region 游戏内事件
        
        /// <summary>
        /// 玩家准备状态更新
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="isReady">是否准备</param>
        /// <returns></returns>
        ValueTask SetReadyStatusAsync(string playerId, bool isReady);
        
        /// <summary>
        /// 游戏开始事件
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <returns></returns>
        ValueTask GameStartAsync(string roomId);
        
        /// <summary>
        /// 游戏结束事件
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="gameResult">游戏结果数据</param>
        /// <returns></returns>
        ValueTask GameEndAsync(string roomId, string gameResult);
        
        #endregion
    }
}