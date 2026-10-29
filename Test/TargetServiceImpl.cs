using Grpc.Core;

namespace Test;

public class TargetServiceImpl : Test.TargetService.TargetServiceBase
{
    private ITargetManager manager;
    private Target? target;

    public TargetServiceImpl(ITargetManager manager)
    {
        this.manager = manager;
    }
    
    public override async Task<ConnectTargetReply> Connect(ConnectTargetRequest request, ServerCallContext context)
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
            Info   = info
        };
    }

    public override async Task<DisconnectTargetReply> Disconnect(DisconnectTargetRequest request, ServerCallContext context)
    {
        if (target == null)
        {
            return new DisconnectTargetReply { 
                Result = TargetResult.NotConnected
            };
        }
        
        /* Disconnect the target. */
        await target.DisconnectAsync();
        
        return new DisconnectTargetReply
        {
            Result = TargetResult.Success
        };
    }

    public override async Task<CloseTargetReply> Close(CloseTargetRequest request, ServerCallContext context)
    {
        if (target == null)
        {
            return new CloseTargetReply { 
                Result = TargetResult.NotConnected
            };
        }
        
        /* Close this reference. */
        target.Close();
        
        return new CloseTargetReply { 
            Result = TargetResult.Success
        };
    }
}