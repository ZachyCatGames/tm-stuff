using Grpc.Core;

namespace Test.protos;

public static class Conversions
{
    private static readonly Dictionary<proto.QueryType, QueryType> _queryTypeToImpl =
        new Dictionary<proto.QueryType, QueryType>
        {
            { proto.QueryType.Connected, QueryType.Connected },
            { proto.QueryType.Disconnected, QueryType.Disconnected },
            { proto.QueryType.All, QueryType.All },
        };
    
    public static QueryType QueryTypeToImpl(proto.QueryType type)
    {
        return _queryTypeToImpl[type];
    }
    
    public static proto.TargetInfoMsg ConvertTargetInfo(TargetInfo info)
    {
        return new proto.TargetInfoMsg
        {
            Address        = info.address,
            SerialNumber   = info.serialNumber,
            ConnectionType = info.connectionType,
        };
    }
}