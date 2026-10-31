namespace TmGui.Models;

using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Grpc.Net.Client;
using System.Reflection.Metadata;

public class TargetProxy
{
    public string SerialNumber { get; init; }

    private static TargetService.TargetServiceClient client;

    public static void Initialize(GrpcChannel channel)
    {
        client = new TargetService.TargetServiceClient(channel);
    }
    
    public TargetProxy(string serialNumber)
    {
        SerialNumber = serialNumber;
    }
    
    public async Task<TargetStatus> GetStatus()
    {
        // TODO
        return TargetStatus.Connected;
    }

    public async Task<(TargetResult, TargetInfo)> Connect()
    {
        var reply = await client.ConnectAsync(
            new ConnectTargetRequest
            {
                SerialNumber = SerialNumber
            }
        );
        
        return (reply.Result, reply.Info);
    }

    public async Task<TargetResult> Disconnect()
    {
        var reply = await client.DisconnectAsync(new DisconnectTargetRequest { SerialNumber = SerialNumber });
        return reply.Result;
    }

    public async Task<(TargetResult, List<htcs.PortInfo>)> GetPortInfos(int maxCount)
    {
        var reply = await client.QueryHtcsPortInfosAsync(new QueryHtcsPortInfosRequest
        {
            SerialNumber = SerialNumber,
            MaxCount = maxCount
        });

        return (reply.Result, reply.Infos.ToList());
    }
}