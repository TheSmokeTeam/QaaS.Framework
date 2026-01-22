using QaaS.Framework.Policies.Exceptions;

namespace QaaS.Framework.Policies;

public abstract class Policy
{
    protected Policy? Next;
    
    // <summary>
    // the place the policy should be in the chain.
    // a lower index means closer to the start.
    // used to create order between policies that have an ordered relationship.
    // </summary>
    protected abstract uint Index { get; set; }
    
    public Policy Add(Policy policy)
    {
        if (Next == null && Index <= policy.Index)
        {
            Next = policy;
            return this;
        }

        if (Index > policy.Index)
        {
            policy.Next = this;
            return policy;
        }

        Next = Add(policy);
        return this;
    }
    
    public void SetupChain()
    {
        SetupThis();
        Next?.SetupThis();
    }

    protected abstract void SetupThis();

    public bool RunChain()
    {
        try {
            RunThis();
        }
        catch (StopActionException) {
            // log exception
            return false;
        }

        if (Next == null)
            return true;
        return Next.RunChain();
    }

    protected abstract void RunThis();
}
