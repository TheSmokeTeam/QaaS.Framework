namespace QaaS.Framework.Policies.AdvancedLoadBalance;

public struct LoadBalanceStage
{
    public double MessagesPerSecond { get; set; }
    public ulong? AmountToNextStage { get; set; }
    public ulong? TimeToNextStage { get; set; }

    public LoadBalanceStage(double rate, ulong intervalMs, ulong? amountToNextStage, ulong? timeToNextStage)
    {
        MessagesPerSecond = rate / intervalMs * 1000;
        AmountToNextStage = amountToNextStage;
        TimeToNextStage = timeToNextStage;
    }
}