using Orleans;

namespace Wind.GrainInterfaces;

/// <summary>
/// 第一个测试Grain接口，用于验证Orleans基础环境
/// </summary>
public interface IHelloGrain : IGrainWithStringKey
{
    /// <summary>
    /// 简单的问候方法，返回包含姓名的问候信息
    /// </summary>
    /// <param name="name">要问候的姓名</param>
    /// <returns>问候消息</returns>
    Task<string> SayHelloAsync(string name);
}