namespace QaaS.Framework.SDK.Session.DataObjects;

/// <summary>
/// Contains the body of the data, along with any additional properties we may want to attach to the input's body.
/// </summary>
/// <typeparam name="T"> Type of data body </typeparam>
public record Data<T>
{
    /// <summary>
    /// The data itself, its contents 
    /// </summary>
    public T? Body { get; init; }
    
    /// <summary>
    /// Meta data of the data that is relevant to certain QaaS actions for example rabbitmq routing key etc..
    /// </summary>
    public MetaDataObjects.MetaData? MetaData { get; init; }
}
