namespace QaaS.Framework.Protocols.ConfigurationObjects.Kafka;

/// <summary>Controls persistence of received offsets, not downstream processing acknowledgements.</summary>
public enum KafkaCommitMode
{
    /// <summary>Synchronous commit for every received record.</summary>
    PerMessage,

    /// <summary>Commit receipt offsets in batches, on idle reads and on orderly disconnect/revocation.</summary>
    Batched,
}
