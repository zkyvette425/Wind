// MongoDB副本集初始化脚本
// 支持事务功能需要副本集模式

// 等待MongoDB完全启动
sleep(3000);

try {
    // 初始化副本集
    var result = rs.initiate({
        _id: "rs0",
        members: [
            {
                _id: 0,
                host: "localhost:27017"
            }
        ]
    });
    
    if (result.ok === 1) {
        print("✅ 副本集初始化成功");
        
        // 等待副本集状态稳定
        sleep(5000);
        
        // 检查副本集状态
        var status = rs.status();
        print("📊 副本集状态:", JSON.stringify(status.members[0].stateStr));
        
        if (status.members[0].stateStr === "PRIMARY") {
            print("🎯 副本集已成为PRIMARY，支持事务功能");
        }
    } else {
        print("❌ 副本集初始化失败:", JSON.stringify(result));
    }
} catch (error) {
    print("⚠️  副本集初始化异常:", error.message);
    
    // 检查是否已经初始化过
    try {
        var status = rs.status();
        if (status.ok === 1) {
            print("ℹ️  副本集已存在，状态:", JSON.stringify(status.members[0].stateStr));
        }
    } catch (statusError) {
        print("🔍 副本集状态检查失败:", statusError.message);
    }
}

print("🏁 副本集初始化脚本执行完成");