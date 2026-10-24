
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Test;
using Test.env;
using Test.hio;
using Test.htcs;
using Test.test;
using static System.Console;

var b = new BeaconPacket("");

async Task<int> SleepFunc()
{

    await System.Threading.Tasks.Task.Delay(1000);
    Console.WriteLine("Cringe");
    return 1;
}

async Task TestFunc()
{
    
    Console.WriteLine("World");
}

async Task CallerFunc()
{
    Console.WriteLine("Test");
    await System.Threading.Tasks.Task.Delay(1000);
    Console.WriteLine("Hello");
}

Task[] tsks = new Task[5];

for (int i = 0; i < 5; i++)
{
    tsks[i] = CallerFunc();
}

Console.WriteLine("Started all");

for (int i = 0; i < 5; i++)
{
    //await tsks[i];
}

Console.WriteLine("All Finished");
//return;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddSingleton<ITargetManager, TargetManagerImpl>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<TargetServiceImpl>();
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();

/*
var packet = new Packet();
var buf = packet.GetBuffer();

//var htcsSrv = new HostHtcsService();

while (true)
{
    usbif.Read(buf, 3000, out int readSize);
    Console.WriteLine("Read packet {0:X}", readSize);

    var msgHeader = packet.GetHeader();

    switch (msgHeader.serviceId)
    {
        case 0xB644D830:
            //htcsSrv.Process(msgHeader, packet);
            break;
        default:
            break;
    }
}
*/

/*
var hdr_b = hdr.ToBytes();
Console.WriteLine("{0}", BitConverter.ToString(hdr_b));

var hdr2 = new Test.PacketHeader(hdr_b);
Console.WriteLine("{0:X}", hdr2.serviceId);

Console.WriteLine("{0}", BitConverter.ToString(BitConverter.GetBytes(a)));
*/
