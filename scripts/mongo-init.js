// MongoDB初始化脚本 - 创建Wind游戏数据库和用户

// 切换到windgame数据库
db = db.getSiblingDB('windgame');

// 创建应用程序用户
db.createUser({
  user: 'windapp',
  pwd: 'windapp123',
  roles: [
    {
      role: 'readWrite',
      db: 'windgame'
    }
  ]
});

// 创建基础集合和索引
db.createCollection('players');
db.createCollection('rooms');
db.createCollection('gamerecords');
db.createCollection('configurations');

// 为players集合创建索引
db.players.createIndex({ "playerId": 1 }, { unique: true });
db.players.createIndex({ "email": 1 }, { unique: true });
db.players.createIndex({ "lastLoginTime": 1 });

// 为rooms集合创建索引  
db.rooms.createIndex({ "roomId": 1 }, { unique: true });
db.rooms.createIndex({ "createdTime": 1 });
db.rooms.createIndex({ "status": 1 });

// 为gamerecords集合创建索引
db.gamerecords.createIndex({ "playerId": 1, "gameTime": -1 });
db.gamerecords.createIndex({ "roomId": 1 });

// 插入默认配置数据
db.configurations.insertOne({
  _id: "server_config",
  maxPlayersPerRoom: 4,
  gameSessionTimeoutMinutes: 30,
  version: "1.0.0",
  createdAt: new Date(),
  updatedAt: new Date()
});

print('Wind游戏数据库初始化完成');
print('- 数据库: windgame');  
print('- 用户: windapp');
print('- 集合: players, rooms, gamerecords, configurations');
print('- 索引: 已创建性能索引');