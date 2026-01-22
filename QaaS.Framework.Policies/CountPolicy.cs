using QaaS.Framework.Policies.Exceptions;

namespace QaaS.Framework.Policies;

public class CountPolicy : Policy
{
    private int _maxAmountOfMessages;
    private int _counter;

    public CountPolicy(int maxAmountOfMessages) => _maxAmountOfMessages = maxAmountOfMessages;

    protected override uint Index { get; set; } = 0;
    
    protected override void SetupThis() => _counter = 0;

    protected override void RunThis()
    {
        _counter++;
        if (_counter >= _maxAmountOfMessages)
            throw new CountStopException(_counter, CommunicationType.read);
    }
}