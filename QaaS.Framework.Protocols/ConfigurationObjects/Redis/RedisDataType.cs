namespace QaaS.Framework.Protocols.ConfigurationObjects.Redis;
/// <summary>
/// Enum that defines the possible redis sending types, supported by the qaas
/// </summary>
public enum RedisDataType
{
    SetString,
    ListLeftPush,
    ListRightPush,
    SetAdd,
    HashSet,
    SortedSetAdd,
    GeoAdd
}