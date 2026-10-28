using Grpc.Core;
using Test.proto;
using Test.protos;

namespace Test;

public class TargetServiceImpl : Test.proto.TargetService.TargetServiceBase
{
    private ITargetManager manager;

    public TargetServiceImpl(ITargetManager manager)
    {
        this.manager = manager;
    }

    public override async Task<QueryTargetCountReply> QueryTargetCount(QueryTargetCountRequest request, ServerCallContext context)
    {
        int count = await manager.QueryTargetCountAsync(protos.Conversions.QueryTypeToImpl(request.Type));
        return new QueryTargetCountReply
        {
            Result = TargetResult.Success,
            Count  = count
        };
    }
    
    public override async Task<QueryTargetInfoReply> QueryTargetInfo(QueryTargetInfoRequest request, ServerCallContext context)
    {
        var info = await manager.QueryTargetInfosAsync(Conversions.QueryTypeToImpl(request.Type), request.MaxCount);

        var convList = info.ConvertAll(Conversions.ConvertTargetInfo);
        var t = new QueryTargetInfoReply
        {
            Result = TargetResult.Success,
        };
        t.Targets.AddRange(convList);

        return t;
    }
    
    public override async Task<ConnectTargetReply> ConnectTarget(ConnectTargetRequest request, ServerCallContext context)
    {
        var info = await manager.ConnectTargetAsync(request.SerialNumber);
        if (info == null)
        {
            return new ConnectTargetReply
            {
                Result = TargetResult.TargetNotFound,
            };
        }

        return new ConnectTargetReply
        {
            Result = TargetResult.Success,
            Info = Conversions.ConvertTargetInfo((TargetInfo)info)
        };
    }

    public override async Task<DisconnectTargetReply> DisconnectTarget(DisconnectTargetRequest request, ServerCallContext context)
    {
        await manager.DisconnectTargetAsync(request.SerialNumber);
        return new DisconnectTargetReply
        {
            Result = TargetResult.Success
        };
    }
}