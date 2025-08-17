using Xunit;

namespace Wind.Tests.TestFixtures;

/// <summary>
/// Orleans集群测试集合定义
/// 确保使用ClusterFixture的测试在同一个集群实例上运行
/// </summary>
[CollectionDefinition("ClusterCollection")]
public class ClusterCollectionDefinition : ICollectionFixture<ClusterFixture>
{
    // 这个类只是一个定义，不需要实现任何内容
    // xUnit会自动为Collection中的所有测试类提供ClusterFixture实例
}