namespace QaaS.Framework.SDK.Session.MetaDataObjects;

/// <summary>
/// Represents the metadata of a redis item
/// </summary>
public record Redis
{
    public string Key { get; init; }
    
    public string? HashField { get; init; }
    
    public double? SetScore { get; init; }
    
    public double? GeoLongitude { get; init; }
    
    public double? GeoLatitude { get; init; }

}