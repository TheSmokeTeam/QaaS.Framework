using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.Hooks.Assertion;

/// <summary>
/// Interface representing an assertion that may be used by QaaS to assert the on sessionData of
/// configured sessions
/// </summary>
public interface IAssertion : IHook
{
    /// <summary>
    /// The message that will be displayed with the assertion
    /// </summary>
    public string? AssertionMessage { get; set; }

    /// <summary>
    /// The trace of the message that will be displayed with the assertion
    /// </summary>
    public string? AssertionTrace { get; set; }

    /// <summary>
    /// The attachments that will be stored and displayed with the assertion
    /// </summary>
    public IList<AssertionAttachment> AssertionAttachments { get; set; }

    /// <summary>
    /// Represents the result of the assertion
    /// </summary>
    public AssertionStatus? AssertionStatus { get; set; }
    
    /// <summary>
    /// Perform assertion on the given sessions with the relevant data sources and configuration
    /// </summary>
    /// <param name="sessionDataList"> The data of the relevant sessions for this assertion scope </param>
    /// <param name="dataSourceList"> The relevant data sources for this data source scope </param>
    /// <returns> True if the assertion passed False if it failed </returns>
    public bool Assert(IImmutableList<SessionData> sessionDataList, IImmutableList<DataSource> dataSourceList);
}