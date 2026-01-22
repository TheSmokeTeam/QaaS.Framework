

using QaaS.Framework.Protocols.ConfigurationObjects;

namespace QaaS.Framework.Protocols.Utils;

/// <summary>
/// this class responsible for generating names by given parameters
/// </summary>
/// <param name="namingType"> how to generate the name </param>
/// <param name="prefix"> prefix for the object name </param>
public class ObjectNameGenerator(ObjectNamingGeneratorType namingType, string prefix)
{
    private int _objectIndex;

    /// <summary>
    /// this function generates object's name
    /// </summary>
    /// <returns> The object name </returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public string GenerateObjectName()
    {
        return namingType switch
        {
            ObjectNamingGeneratorType.RandomGuid => prefix + Guid.NewGuid(),
            ObjectNamingGeneratorType.GrowingNumericalSeries => prefix + _objectIndex++,
            _ => throw new ArgumentOutOfRangeException(nameof(namingType), namingType,
                "object sender naming type mechanism is not supported")
        };
    }
}