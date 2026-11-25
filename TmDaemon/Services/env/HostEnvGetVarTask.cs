

using System.Text;

namespace Test.Services.env;

public class HostEnvGetVarTask : ServiceTask
{
    public HostEnvGetVarTask(IService parent, uint taskId) : base(parent, TaskType.GetVar, taskId, 0)
    {}

    protected override async Task Run()
    {
        /* Get packet. */
        Packets.Packet pkt = await WaitForPacket();
        
        Console.WriteLine("[env] GetEnvVar");
        
        /* Parse the packet. */
        string varName = pkt.ReadString(Packets.Packet.DataMaxSize);
        
        /* Get the env var. */
        string? varValue = HostEnvManager.GetEnvironmentVariable(varName);
        
        /* Setup a reply. */
        Packets.Packet reply = await AllocSendPacketAsync();
        reply.isInitiate = false;

        /* Check if a var was found. */
        if (varValue is string value)
        {
            reply.Write((Int32)EnvErrorCode.Success);
            reply.Write(Encoding.UTF8.GetBytes(value));
        }
        else
        {
            reply.Write((Int32)EnvErrorCode.NotFound);
        }

        /* Send it. */
        reply.WriteHeader();
        reply.Print();
        await SendPacketAsync(reply);
    }
}