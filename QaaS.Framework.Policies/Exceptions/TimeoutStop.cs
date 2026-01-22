namespace QaaS.Framework.Policies.Exceptions;

public class TimeoutStopException : StopActionException
{
    public TimeoutStopException(TimeSpan setTime) 
        : base($"Stop: the action ran for more then the set timeout of {setTime.Milliseconds} milliseconds.") { }
}