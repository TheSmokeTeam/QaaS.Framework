
namespace QaaS.Framework.Policies.Exceptions;

public class CountStopException : StopActionException
{
    public CountStopException(int msgAmount, CommunicationType comType) 
        : base($"Stop: {msgAmount} messages were {comType.ToString()} - max amount reached.") { }
}

public enum CommunicationType
{
    read,
    sent
}