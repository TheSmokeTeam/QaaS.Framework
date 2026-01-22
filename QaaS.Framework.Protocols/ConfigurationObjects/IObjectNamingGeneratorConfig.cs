
namespace QaaS.Framework.Protocols.ConfigurationObjects;

public interface IObjectNamingGeneratorConfig 
{
    public string Prefix { get; set; }

    public ObjectNamingGeneratorType NamingType { get; set; }
}