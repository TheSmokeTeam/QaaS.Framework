namespace QaaS.Framework.Policies.Exceptions;

public class StopActionException : Exception
{
    public StopActionException(string policyStopMessage) : base(policyStopMessage) { }
}