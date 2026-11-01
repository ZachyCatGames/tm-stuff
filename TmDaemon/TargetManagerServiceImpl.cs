namespace Test;
using Grpc.Core;

public class TargetManagerServiceImpl : TargetManagerService.TargetManagerServiceBase
{
    private ITargetManager manager;
    
    public TargetManagerServiceImpl(ITargetManager manager)
    {
        this.manager = manager;
    }

    public override async Task<InitManagerClientContextReply> InitializeUserContext(InitManagerClientContextRequest request, ServerCallContext context)
    {
        int contextId = await manager.InitializeClientContext();
        if (contextId < 0)
        {
            /* TODO: What result should this be? */
            return new InitManagerClientContextReply()
            {
                Result = TargetResult.Success,
                ContextId = -1,
            };
        }

        return new InitManagerClientContextReply()
        {
            Result = TargetResult.Success,
            ContextId = contextId
        };
    }
    
    public override async Task<FiniManagerClientContextReply> FinalizeUserContext(FiniManagerClientContextRequest request, ServerCallContext context)
    {
        TargetResult res = await manager.FinalizeClientContext(request.ContextId);

        return new FiniManagerClientContextReply()
        {
            Result    = res,
            ContextId = request.ContextId
        };
    }

    public override async Task<QueryTargetCountReply> QueryTargetCount(QueryTargetCountRequest request, ServerCallContext context)
    {
        int count = await manager.QueryTargetCountAsync(request.Type);
        return new QueryTargetCountReply
        {
            Result = TargetResult.Success,
            Count  = count
        };
    }
    
    public override async Task<QueryTargetInfoReply> QueryTargetInfo(QueryTargetInfoRequest request, ServerCallContext context)
    {
        var info = await manager.QueryTargetInfosAsync(request.Type, request.MaxCount);

        var t = new QueryTargetInfoReply
        {
            Result = TargetResult.Success,
        };
        t.Targets.AddRange(info);

        return t;
    }

    public override async Task<QueryTargetInfoBySerialReply> QueryTargetInfoBySerial(QueryTargetInfoBySerialRequest request, ServerCallContext context)
    {
        var info = await manager.QueryTargetInfoBySerialAsync(request.SerialNumber);

        if (info == null)
        {
            return new QueryTargetInfoBySerialReply()
            {
                Result = TargetResult.TargetNotFound
            };
        }

        return new QueryTargetInfoBySerialReply()
        {
            Result = TargetResult.Success,
            Info = info
        };
    }

    public override async Task<GetTargetUpdateReply> GetTargetUpdate(GetTargetUpdateRequest request, ServerCallContext context)
    {
        TargetInfo? info = null;
        TargetResult result;
        try
        {
            (result, info) = await manager.GetTargetUpdateAsync(request.ContextId, context.CancellationToken);
        }
        catch (OperationCanceledException e)
        {
            return new GetTargetUpdateReply()
            {
                Result = TargetResult.OperationCanceled
            };
        }

        if (result != TargetResult.Success)
        {
            return new GetTargetUpdateReply()
            {
                Result = result,
            };
        }

        return new GetTargetUpdateReply()
        {
            Result = TargetResult.Success,
            Info   = info
        };
    }
}