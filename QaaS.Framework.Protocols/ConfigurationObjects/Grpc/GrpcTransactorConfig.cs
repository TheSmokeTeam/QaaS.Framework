using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Grpc;

public record GrpcTransactorConfig: ITransactorConfig
{
    [Required, Description("Grpc server host")]
    public string? Host { get; set; }
    
    [Required, Range(ushort.MinValue, ushort.MaxValue), Description("Grpc server port")]
    public ushort? Port { get; set; }
    
    [Required, Description("The name of the assembly the grpc protos are defined in")]
    public string? AssemblyName { get; set; }

    [Required, Description("The namespace the grpc protos are defined in")]
    public string?  ProtoNameSpace { get; set; }
    
    [Required, Description("The name of the service the rpc is defined in")]
    public string? ServiceName { get; set; }
    
    [Required, Description("The name of the rpc to invoke")]
    public string? RpcName { get; set; }

}