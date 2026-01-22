using System.Reflection;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Grpc;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public class GrpcProtocol : ITransactor
{
    private readonly ILogger _logger;
    private const string GrpcServiceClientSuffix = "Client";
    private readonly ClientBase _serviceClient;
    private readonly MethodInfo _rpcMethod;
    private TimeSpan? _timeout;

    public GrpcProtocol(GrpcTransactorConfig configuration, ILogger logger, TimeSpan timeout)
    {
        _logger = logger;
        _timeout = timeout;

        var channel = new Channel($"{configuration.Host}:{configuration.Port}", ChannelCredentials.Insecure);

        var assembly = Assembly.Load(configuration.AssemblyName!);

        // Get service client
        var serviceType = assembly.GetType($"{configuration.ProtoNameSpace!}.{configuration.ServiceName!}",
            throwOnError: true)!;
        var serviceClientType = serviceType.GetNestedType($"{configuration.ServiceName!}{GrpcServiceClientSuffix}",
            BindingFlags.Public | BindingFlags.NonPublic)!;
        _serviceClient = (ClientBase)Activator.CreateInstance(serviceClientType, channel)!;

        // Get service client method
        _rpcMethod = serviceClientType
                         .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .FirstOrDefault(m =>
                             m.Name == configuration.RpcName!
                             && m.GetParameters().Length == 2
                             && m.GetParameters()[1].ParameterType == typeof(CallOptions)) ??
                     throw new ArgumentException(
                         $"Could not find rpc method {configuration.RpcName!} in service client {serviceClientType}");
    }

    /// <inheritdoc />
    public SerializationType? GetInputCommunicationSerializationType() => SerializationType.ProtobufMessage;

    /// <inheritdoc />
    public SerializationType? GetOutputCommunicationSerializationType() => SerializationType.ProtobufMessage;


    public Tuple<DetailedData<object>, DetailedData<object>?> Transact(Data<object> dataToSend)
    {
        // Send data
        var requestUtcTime = DateTime.UtcNow;
        IMessage? responseData;
        try
        {
            responseData = (IMessage?)_rpcMethod.Invoke(_serviceClient, [
                dataToSend.Body ?? throw new ArgumentException(
                    "A data item's body is null, can't send it as a proto msg using grpc"),
                new CallOptions(deadline: DateTime.UtcNow + _timeout)
            ]);
        }
        catch (TargetInvocationException e)
        {
            if (e.InnerException is not RpcException { StatusCode: StatusCode.DeadlineExceeded })
                throw;
            _logger.LogDebug("Timeout exceeded when performing a grpc request, no response saved");
            return new Tuple<DetailedData<object>, DetailedData<object>?>(
                dataToSend.CloneDetailed(requestUtcTime), null)!;
        }

        var responseUtcTime = DateTime.UtcNow;

        return new Tuple<DetailedData<object>, DetailedData<object>?>(dataToSend.CloneDetailed(requestUtcTime),
            new()
            {
                Body = responseData,
                Timestamp = responseUtcTime
            })!;
    }
}