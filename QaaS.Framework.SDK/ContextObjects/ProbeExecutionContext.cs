using System.Runtime.CompilerServices;

namespace QaaS.Framework.SDK.ContextObjects;

/// <summary>
/// Describes the currently executing probe within a specific QaaS context.
/// </summary>
/// <param name="SessionName">The session that owns the running probe.</param>
/// <param name="ProbeName">The configured action name of the running probe.</param>
public readonly record struct ProbeExecutionDescriptor(string SessionName, string ProbeName);

/// <summary>
/// Carries the currently executing probe identity through async flows.
/// Common probe packages use this scope to derive deterministic global-dictionary paths that are unique per
/// execution, session, and probe name without introducing a dependency on runner assemblies.
/// </summary>
public static class ProbeExecutionContext
{
    private static readonly AsyncLocal<ProbeExecutionScopeState?> CurrentScope = new();

    /// <summary>
    /// Enters a probe execution scope for the supplied <paramref name="context"/>.
    /// The returned scope must be disposed after the probe finishes so nested or sequential probe executions restore
    /// the previous scope correctly.
    /// </summary>
    public static IDisposable Enter(Context context, string sessionName, string probeName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(probeName);

        var previousScope = CurrentScope.Value;
        CurrentScope.Value = new ProbeExecutionScopeState(context, new ProbeExecutionDescriptor(sessionName, probeName));
        return new ScopeRestorer(previousScope);
    }

    /// <summary>
    /// Returns the current probe execution descriptor when the active async scope belongs to the supplied
    /// <paramref name="context"/>.
    /// </summary>
    public static bool TryGetCurrent(Context context, out ProbeExecutionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentScope = CurrentScope.Value;
        if (!ReferenceEquals(currentScope?.Context, context))
        {
            descriptor = default;
            return false;
        }

        descriptor = currentScope.Descriptor;
        return true;
    }

    /// <summary>
    /// Returns the current probe execution descriptor for the supplied <paramref name="context"/>, or throws when the
    /// call happens outside a runner-managed probe execution scope.
    /// </summary>
    public static ProbeExecutionDescriptor GetCurrent(Context context)
    {
        if (TryGetCurrent(context, out var descriptor))
        {
            return descriptor;
        }

        throw new InvalidOperationException(
            $"No active probe execution scope is available for context {RuntimeHelpers.GetHashCode(context):X8}.");
    }

    private sealed record ProbeExecutionScopeState(Context Context, ProbeExecutionDescriptor Descriptor);

    private sealed class ScopeRestorer(ProbeExecutionScopeState? previousScope) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentScope.Value = previousScope;
            _disposed = true;
        }
    }
}
