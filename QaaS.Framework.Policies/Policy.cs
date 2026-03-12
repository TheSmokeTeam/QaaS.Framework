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
    
    /// <summary>
    /// Inserts a policy into the chain while preserving ascending <see cref="Index"/> order.
    /// </summary>
    public Policy Add(Policy policy)
    {
        var next = Next;
        if (next == null && Index <= policy.Index)
        {
            Next = policy;
            return this;
        }

        if (Index > policy.Index)
        {
            policy.Next = this;
            return policy;
        }

        // Recurse through the remaining chain instead of re-adding against the current node,
        // otherwise chains longer than two items can loop back into this instance indefinitely.
        ArgumentNullException.ThrowIfNull(next);
        Next = next.Add(policy);
        return this;
    }
    
    /// <summary>
    /// Initializes the current policy and every remaining policy in the chain.
    /// </summary>
    public void SetupChain()
    {
        SetupThis();
        Next?.SetupChain();
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
