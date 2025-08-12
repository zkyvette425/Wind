using MagicOnion;

namespace Wind.Shared.Services;

/// <summary>
/// 测试RPC服务接口 - 验证MagicOnion集成
/// 基于Context7文档的MagicOnion服务定义规范
/// </summary>
public interface ITestService : IService<ITestService>
{
    /// <summary>
    /// 简单加法运算 - 测试基础RPC调用
    /// </summary>
    /// <param name="x">第一个操作数</param>
    /// <param name="y">第二个操作数</param>
    /// <returns>两数之和</returns>
    UnaryResult<int> AddAsync(int x, int y);

    /// <summary>
    /// 字符串回显 - 测试字符串序列化
    /// </summary>
    /// <param name="message">输入消息</param>
    /// <returns>服务器回显的消息</returns>
    UnaryResult<string> EchoAsync(string message);

    /// <summary>
    /// 获取服务器信息 - 测试无参数调用
    /// </summary>
    /// <returns>服务器信息字符串</returns>
    UnaryResult<string> GetServerInfoAsync();
}