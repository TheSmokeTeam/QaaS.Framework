using System.Reflection;
using Moq;
using Moq.Protected;
using ITimer = QaaS.Framework.Policies.Extentions.Stopwatch.ITimer;

namespace QaaS.Framework.Policies.Tests;

[TestFixture]
public class LoadBalancePolicyTests
{
    private readonly Mock<ITimer>? _mockTimer = new();
    private Mock<LoadBalancePolicy>? _mockBaseLoadBalancePolicy;
    
    private readonly FieldInfo? _messageIntervalMsField =
        typeof(LoadBalancePolicy).GetField("MessageIntervalMilliseconds",
            BindingFlags.NonPublic | BindingFlags.Instance);

    [Test,
     TestCase(5, 250UL, 100UL),
     TestCase(100, 20UL, 10UL)]
    public void TestAdjustRate_CallMethod_ExpectIntervalMilliSecondsFieldToChangeByRemainingTimeAndCount(double rate,
        ulong timeIntervalMs, ulong elapsedMs)
    {
        // Arrange
        _mockTimer!.SetupGet(m => m.ElapsedMilliseconds).Returns((long)elapsedMs);
        _mockBaseLoadBalancePolicy = new Mock<LoadBalancePolicy>(rate, timeIntervalMs, _mockTimer.Object);
        _mockBaseLoadBalancePolicy.Protected().Setup("AdjustRate").CallBase().Verifiable();
        var loadBalancePolicy = _mockBaseLoadBalancePolicy.Object;

        var messagesPerSecond = rate / timeIntervalMs * 1000;
        var wantedIntervalMs =
            (1000 - (elapsedMs - (double)_messageIntervalMsField!.GetValue(loadBalancePolicy)!)) /
            messagesPerSecond;

        // Act
        loadBalancePolicy!.GetType().GetMethod("AdjustRate", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(loadBalancePolicy, null);

        // Assert
        _mockBaseLoadBalancePolicy.Protected().Verify("AdjustRate", Times.Once());
        Assert.That(_messageIntervalMsField!.GetValue(loadBalancePolicy), Is.EqualTo(wantedIntervalMs));
    }
}