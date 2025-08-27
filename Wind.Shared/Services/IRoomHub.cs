using MagicOnion;
using System;
using System.Threading.Tasks;

namespace Wind.Shared.Services
{
    /// <summary>
    /// 房间StreamingHub接口 - 专门处理房间实时状态同步和游戏事件推送
    /// 支持房间状态管理、玩家动作同步、游戏事件分发等功能
    /// </summary>
    public interface IRoomHub : IStreamingHub<IRoomHub, IRoomHubReceiver>
    {
        #region 房间连接管理

        /// <summary>
        /// 连接房间Hub服务
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="roomId">房间ID</param>
        /// <param name="accessToken">JWT访问令牌</param>
        /// <returns></returns>
        ValueTask ConnectToRoomAsync(string playerId, string roomId, string accessToken);

        /// <summary>
        /// 断开房间Hub连接
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="roomId">房间ID</param>
        /// <returns></returns>
        ValueTask DisconnectFromRoomAsync(string playerId, string roomId);

        /// <summary>
        /// 房间心跳检测
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="roomId">房间ID</param>
        /// <returns>服务器时间戳</returns>
        ValueTask<long> RoomHeartbeatAsync(string playerId, string roomId);

        #endregion

        #region 房间状态同步

        /// <summary>
        /// 更新房间设置
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="ownerId">房主ID</param>
        /// <param name="settingsJson">房间设置 (JSON格式)</param>
        /// <returns></returns>
        ValueTask UpdateRoomSettingsAsync(string roomId, string ownerId, string settingsJson);

        /// <summary>
        /// 更新房间状态
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="newStatus">新状态 (Waiting/Starting/Playing/Paused/Finished)</param>
        /// <param name="updaterId">更新者ID</param>
        /// <returns></returns>
        ValueTask UpdateRoomStatusAsync(string roomId, string newStatus, string updaterId);

        /// <summary>
        /// 获取完整房间状态
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="requesterId">请求者ID</param>
        /// <returns></returns>
        ValueTask RequestFullRoomStateAsync(string roomId, string requesterId);

        #endregion

        #region 玩家状态同步

        /// <summary>
        /// 更新玩家准备状态
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="isReady">是否准备</param>
        /// <returns></returns>
        ValueTask UpdatePlayerReadyStatusAsync(string roomId, string playerId, bool isReady);

        /// <summary>
        /// 更新玩家位置信息
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="positionData">位置数据 (JSON格式)</param>
        /// <returns></returns>
        ValueTask UpdatePlayerPositionAsync(string roomId, string playerId, string positionData);

        /// <summary>
        /// 更新玩家游戏状态
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="gameState">游戏状态数据 (JSON格式)</param>
        /// <returns></returns>
        ValueTask UpdatePlayerGameStateAsync(string roomId, string playerId, string gameState);

        /// <summary>
        /// 设置玩家观察者模式
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="isObserver">是否为观察者</param>
        /// <returns></returns>
        ValueTask SetPlayerObserverModeAsync(string roomId, string playerId, bool isObserver);

        #endregion

        #region 游戏流程同步

        /// <summary>
        /// 开始游戏倒计时
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="ownerId">房主ID</param>
        /// <param name="countdownSeconds">倒计时秒数</param>
        /// <returns></returns>
        ValueTask StartGameCountdownAsync(string roomId, string ownerId, int countdownSeconds);

        /// <summary>
        /// 取消游戏倒计时
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="ownerId">房主ID</param>
        /// <returns></returns>
        ValueTask CancelGameCountdownAsync(string roomId, string ownerId);

        /// <summary>
        /// 开始游戏
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="ownerId">房主ID</param>
        /// <param name="gameConfig">游戏配置 (JSON格式)</param>
        /// <returns></returns>
        ValueTask StartGameAsync(string roomId, string ownerId, string gameConfig);

        /// <summary>
        /// 结束游戏
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="requesterId">请求者ID</param>
        /// <param name="gameResult">游戏结果 (JSON格式)</param>
        /// <returns></returns>
        ValueTask EndGameAsync(string roomId, string requesterId, string gameResult);

        /// <summary>
        /// 暂停游戏
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="requesterId">请求者ID</param>
        /// <param name="reason">暂停原因</param>
        /// <returns></returns>
        ValueTask PauseGameAsync(string roomId, string requesterId, string reason);

        /// <summary>
        /// 恢复游戏
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="requesterId">请求者ID</param>
        /// <returns></returns>
        ValueTask ResumeGameAsync(string roomId, string requesterId);

        #endregion

        #region 玩家行动同步

        /// <summary>
        /// 提交玩家行动
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="actionType">行动类型</param>
        /// <param name="actionData">行动数据 (JSON格式)</param>
        /// <param name="timestamp">行动时间戳</param>
        /// <returns></returns>
        ValueTask SubmitPlayerActionAsync(string roomId, string playerId, string actionType, string actionData, long timestamp);

        /// <summary>
        /// 撤销玩家行动
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="actionId">行动ID</param>
        /// <returns></returns>
        ValueTask UndoPlayerActionAsync(string roomId, string playerId, string actionId);

        /// <summary>
        /// 确认回合结束
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="turnId">回合ID</param>
        /// <returns></returns>
        ValueTask ConfirmTurnEndAsync(string roomId, string playerId, string turnId);

        #endregion

        #region 实时数据同步

        /// <summary>
        /// 同步游戏对象状态
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="objectId">对象ID</param>
        /// <param name="objectType">对象类型</param>
        /// <param name="objectState">对象状态 (JSON格式)</param>
        /// <param name="updaterId">更新者ID</param>
        /// <returns></returns>
        ValueTask SyncGameObjectStateAsync(string roomId, string objectId, string objectType, string objectState, string updaterId);

        /// <summary>
        /// 广播游戏事件
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据 (JSON格式)</param>
        /// <param name="sourcePlayerId">事件源玩家ID</param>
        /// <returns></returns>
        ValueTask BroadcastGameEventAsync(string roomId, string eventType, string eventData, string sourcePlayerId);

        /// <summary>
        /// 请求同步检查
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="checksum">客户端状态校验和</param>
        /// <returns></returns>
        ValueTask RequestSyncCheckAsync(string roomId, string playerId, string checksum);

        #endregion

        #region 观察者功能

        /// <summary>
        /// 开始观察房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="observerId">观察者ID</param>
        /// <param name="accessToken">访问令牌</param>
        /// <returns></returns>
        ValueTask StartObservingRoomAsync(string roomId, string observerId, string accessToken);

        /// <summary>
        /// 停止观察房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="observerId">观察者ID</param>
        /// <returns></returns>
        ValueTask StopObservingRoomAsync(string roomId, string observerId);

        /// <summary>
        /// 切换观察视角
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="observerId">观察者ID</param>
        /// <param name="targetPlayerId">目标玩家ID (null为全局视角)</param>
        /// <returns></returns>
        ValueTask SwitchObserverViewAsync(string roomId, string observerId, string? targetPlayerId);

        #endregion

        #region 房间管理功能

        /// <summary>
        /// 踢出玩家
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="ownerId">房主ID</param>
        /// <param name="targetPlayerId">目标玩家ID</param>
        /// <param name="reason">踢出原因</param>
        /// <returns></returns>
        ValueTask KickPlayerFromRoomAsync(string roomId, string ownerId, string targetPlayerId, string reason);

        /// <summary>
        /// 转移房主权限
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="currentOwnerId">当前房主ID</param>
        /// <param name="newOwnerId">新房主ID</param>
        /// <returns></returns>
        ValueTask TransferRoomOwnershipAsync(string roomId, string currentOwnerId, string newOwnerId);

        /// <summary>
        /// 设置房间权限
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="ownerId">房主ID</param>
        /// <param name="playerId">目标玩家ID</param>
        /// <param name="permissions">权限设置 (JSON格式)</param>
        /// <returns></returns>
        ValueTask SetPlayerPermissionsAsync(string roomId, string ownerId, string playerId, string permissions);

        #endregion
    }
}

namespace Wind.Shared.Services
{
    /// <summary>
    /// 房间Hub接收器接口 - 服务器向客户端推送房间相关消息
    /// </summary>
    public interface IRoomHubReceiver
    {
        #region 连接事件

        /// <summary>
        /// 房间Hub连接成功
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="roomId">房间ID</param>
        /// <param name="serverTime">服务器时间</param>
        void OnRoomHubConnected(string playerId, string roomId, long serverTime);

        /// <summary>
        /// 房间Hub连接断开
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="reason">断开原因</param>
        void OnRoomHubDisconnected(string roomId, string reason);

        /// <summary>
        /// 房间心跳响应
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="serverTime">服务器时间戳</param>
        void OnRoomHeartbeatResponse(string roomId, long serverTime);

        #endregion

        #region 房间状态事件

        /// <summary>
        /// 房间状态更新
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="newStatus">新状态</param>
        /// <param name="updaterId">更新者ID</param>
        /// <param name="timestamp">更新时间</param>
        void OnRoomStatusUpdate(string roomId, string newStatus, string updaterId, long timestamp);

        /// <summary>
        /// 房间设置更新
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="settingsJson">房间设置 (JSON格式)</param>
        /// <param name="updaterId">更新者ID</param>
        void OnRoomSettingsUpdate(string roomId, string settingsJson, string updaterId);

        /// <summary>
        /// 完整房间状态推送
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="roomStateJson">完整房间状态 (JSON格式)</param>
        /// <param name="timestamp">状态时间戳</param>
        void OnFullRoomState(string roomId, string roomStateJson, long timestamp);

        #endregion

        #region 玩家事件

        /// <summary>
        /// 玩家加入房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="playerName">玩家昵称</param>
        /// <param name="playerData">玩家数据 (JSON格式)</param>
        void OnPlayerJoinedRoom(string roomId, string playerId, string playerName, string playerData);

        /// <summary>
        /// 玩家离开房间
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="playerName">玩家昵称</param>
        /// <param name="reason">离开原因</param>
        void OnPlayerLeftRoom(string roomId, string playerId, string playerName, string reason);

        /// <summary>
        /// 玩家准备状态更新
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="playerName">玩家昵称</param>
        /// <param name="isReady">是否准备</param>
        void OnPlayerReadyStatusUpdate(string roomId, string playerId, string playerName, bool isReady);

        /// <summary>
        /// 玩家位置更新
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="positionData">位置数据 (JSON格式)</param>
        /// <param name="timestamp">更新时间</param>
        void OnPlayerPositionUpdate(string roomId, string playerId, string positionData, long timestamp);

        /// <summary>
        /// 玩家游戏状态更新
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="gameState">游戏状态 (JSON格式)</param>
        /// <param name="timestamp">更新时间</param>
        void OnPlayerGameStateUpdate(string roomId, string playerId, string gameState, long timestamp);

        #endregion

        #region 游戏流程事件

        /// <summary>
        /// 游戏倒计时开始
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="countdownSeconds">倒计时秒数</param>
        /// <param name="startTime">开始时间</param>
        void OnGameCountdownStart(string roomId, int countdownSeconds, long startTime);

        /// <summary>
        /// 游戏倒计时更新
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="remainingSeconds">剩余秒数</param>
        void OnGameCountdownUpdate(string roomId, int remainingSeconds);

        /// <summary>
        /// 游戏倒计时取消
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="reason">取消原因</param>
        void OnGameCountdownCancel(string roomId, string reason);

        /// <summary>
        /// 游戏开始
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="gameConfig">游戏配置 (JSON格式)</param>
        /// <param name="startTime">开始时间</param>
        void OnGameStart(string roomId, string gameConfig, long startTime);

        /// <summary>
        /// 游戏结束
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="gameResult">游戏结果 (JSON格式)</param>
        /// <param name="endTime">结束时间</param>
        void OnGameEnd(string roomId, string gameResult, long endTime);

        /// <summary>
        /// 游戏暂停
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="reason">暂停原因</param>
        /// <param name="pauseTime">暂停时间</param>
        void OnGamePause(string roomId, string reason, long pauseTime);

        /// <summary>
        /// 游戏恢复
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="resumeTime">恢复时间</param>
        void OnGameResume(string roomId, long resumeTime);

        #endregion

        #region 玩家行动事件

        /// <summary>
        /// 玩家行动提交
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="actionId">行动ID</param>
        /// <param name="actionType">行动类型</param>
        /// <param name="actionData">行动数据 (JSON格式)</param>
        /// <param name="timestamp">行动时间</param>
        void OnPlayerActionSubmit(string roomId, string playerId, string actionId, string actionType, string actionData, long timestamp);

        /// <summary>
        /// 玩家行动撤销
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="actionId">行动ID</param>
        /// <param name="reason">撤销原因</param>
        void OnPlayerActionUndo(string roomId, string playerId, string actionId, string reason);

        /// <summary>
        /// 回合结束确认
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="turnId">回合ID</param>
        void OnTurnEndConfirm(string roomId, string playerId, string turnId);

        /// <summary>
        /// 回合切换
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="previousPlayerId">上一个玩家ID</param>
        /// <param name="currentPlayerId">当前玩家ID</param>
        /// <param name="turnId">新回合ID</param>
        /// <param name="turnTimeoutSeconds">回合超时时间</param>
        void OnTurnSwitch(string roomId, string previousPlayerId, string currentPlayerId, string turnId, int turnTimeoutSeconds);

        #endregion

        #region 实时数据事件

        /// <summary>
        /// 游戏对象状态同步
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="objectId">对象ID</param>
        /// <param name="objectType">对象类型</param>
        /// <param name="objectState">对象状态 (JSON格式)</param>
        /// <param name="updaterId">更新者ID</param>
        /// <param name="timestamp">更新时间</param>
        void OnGameObjectStateSync(string roomId, string objectId, string objectType, string objectState, string updaterId, long timestamp);

        /// <summary>
        /// 游戏事件广播
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="eventId">事件ID</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据 (JSON格式)</param>
        /// <param name="sourcePlayerId">事件源玩家ID</param>
        /// <param name="timestamp">事件时间</param>
        void OnGameEventBroadcast(string roomId, string eventId, string eventType, string eventData, string sourcePlayerId, long timestamp);

        /// <summary>
        /// 同步检查结果
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">请求玩家ID</param>
        /// <param name="isSynced">是否同步</param>
        /// <param name="serverChecksum">服务器校验和</param>
        /// <param name="desyncDetails">失步详情 (JSON格式)</param>
        void OnSyncCheckResult(string roomId, string playerId, bool isSynced, string serverChecksum, string? desyncDetails);

        #endregion

        #region 观察者事件

        /// <summary>
        /// 观察者加入
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="observerId">观察者ID</param>
        /// <param name="observerName">观察者昵称</param>
        void OnObserverJoined(string roomId, string observerId, string observerName);

        /// <summary>
        /// 观察者离开
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="observerId">观察者ID</param>
        /// <param name="observerName">观察者昵称</param>
        void OnObserverLeft(string roomId, string observerId, string observerName);

        /// <summary>
        /// 观察视角切换
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="observerId">观察者ID</param>
        /// <param name="targetPlayerId">目标玩家ID</param>
        /// <param name="viewType">视角类型</param>
        void OnObserverViewSwitch(string roomId, string observerId, string? targetPlayerId, string viewType);

        #endregion

        #region 管理事件

        /// <summary>
        /// 玩家被踢出
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="kickedPlayerId">被踢玩家ID</param>
        /// <param name="kickedPlayerName">被踢玩家昵称</param>
        /// <param name="kickerId">踢人者ID</param>
        /// <param name="reason">踢出原因</param>
        void OnPlayerKicked(string roomId, string kickedPlayerId, string kickedPlayerName, string kickerId, string reason);

        /// <summary>
        /// 房主权限转移
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="previousOwnerId">前房主ID</param>
        /// <param name="newOwnerId">新房主ID</param>
        /// <param name="newOwnerName">新房主昵称</param>
        void OnOwnershipTransfer(string roomId, string previousOwnerId, string newOwnerId, string newOwnerName);

        /// <summary>
        /// 玩家权限更新
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="permissions">新权限 (JSON格式)</param>
        /// <param name="updaterId">更新者ID</param>
        void OnPlayerPermissionsUpdate(string roomId, string playerId, string permissions, string updaterId);

        #endregion

        #region 错误和警告事件

        /// <summary>
        /// 房间错误通知
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="errorCode">错误代码</param>
        /// <param name="errorMessage">错误信息</param>
        void OnRoomError(string roomId, string errorCode, string errorMessage);

        /// <summary>
        /// 房间警告通知
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="warningType">警告类型</param>
        /// <param name="warningMessage">警告信息</param>
        void OnRoomWarning(string roomId, string warningType, string warningMessage);

        #endregion
    }
}