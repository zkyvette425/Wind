// MongoDB初始化脚本 - 创建测试数据库和集合
print('开始初始化MongoDB测试数据库...');

// 切换到测试数据库
db = db.getSiblingDB('windgame_test');

// 创建测试用户
db.createUser({
  user: 'windapp',
  pwd: 'windapp123',
  roles: [
    {
      role: 'readWrite',
      db: 'windgame_test'
    }
  ]
});

// 创建集合并添加索引
const collections = [
  'Players',
  'Rooms', 
  'GameRecords',
  'Messages',
  'ChatMessages',
  'SystemMessages',
  'Leaderboards'
];

collections.forEach(collectionName => {
  print(`创建集合: ${collectionName}`);
  db.createCollection(collectionName);
  
  // 为每个集合添加基础索引
  if (collectionName === 'Players') {
    db[collectionName].createIndex({ "PlayerId": 1 }, { unique: true });
    db[collectionName].createIndex({ "Email": 1 }, { unique: true });
    db[collectionName].createIndex({ "LastLoginTime": -1 });
  } else if (collectionName === 'Rooms') {
    db[collectionName].createIndex({ "RoomId": 1 }, { unique: true });
    db[collectionName].createIndex({ "CreatedTime": -1 });
    db[collectionName].createIndex({ "Status": 1 });
  } else if (collectionName === 'GameRecords') {
    db[collectionName].createIndex({ "GameId": 1 }, { unique: true });
    db[collectionName].createIndex({ "RoomId": 1 });
    db[collectionName].createIndex({ "StartTime": -1 });
  } else {
    // 其他集合的通用索引
    db[collectionName].createIndex({ "CreatedTime": -1 });
  }
});

print('MongoDB测试数据库初始化完成!');