
using System.Text;
using System.Threading;
using System.Threading;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using Test;
using Test.hio;
using Test.htcs;
using static System.Console;

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

const int productId = 0x3000;
const int vendorId = 0x57E;

var usb = new UsbContext();

// Get a list of connected devices
var usbDevices = usb.List();

// Find a device with the target PID & VID
var selectedDevice = usbDevices.FirstOrDefault(d => d.ProductId == productId && d.VendorId == vendorId);

// Open the device.
selectedDevice.Open();

foreach (var c in selectedDevice.Configs)
{
    Console.WriteLine(c.ToString());
}

foreach (var c in selectedDevice.Configs[0].Interfaces)
{
    Console.WriteLine(c.ToString());
}

/* Create services manager. */
var serviceMgr = new ServiceManager();

/* Initialize USB Interface. */
var usbif = new UsbInterface(selectedDevice);
var pktMgr = new UsbPacketManager(usbif, serviceMgr);

/* Initialize services manager. */
serviceMgr.Init(pktMgr);

/* Initialize host fs manager. */
HostFilesystemManager hostfsMgr = new();

/* Initialize services. */
serviceMgr.RegisterService(new HostHtcsService(serviceMgr, new HostPortManager()));
serviceMgr.RegisterService(new HostIOService(serviceMgr, hostfsMgr));
serviceMgr.RegisterService(new HostDirectoryIOService(serviceMgr, hostfsMgr));

/* Send end connection packet. */
var packet = serviceMgr.AllocRecvPacket();
var hdr = new Test.PacketHeader(0x3a8ddd94, 0, 0, 0, 0);
hdr.WriteTo(packet.GetBuffer());

usbif.Write(packet.GetBuffer(), 0, 0x20, 0, out int writeHdrSize);
Console.WriteLine("Sent header");
//Console.WriteLine(BitConverter.ToString(hdr.ToBytes()));

usbif.Read(packet.GetBuffer(), 0, out int readSize);
Console.WriteLine("Read response");
packet.ParseHeader();

/* Send start connection packet. */
//byte[] initMagic = [0xB8, 0xD1, 0x5E, 0xCD];
hdr = new Test.PacketHeader(0xCD5ED1B8, 0, 0, 0, 0);
hdr.WriteTo(packet.GetBuffer());

usbif.Write(packet.GetBuffer(), 0, 0x20, 0, out int writeHdrSize2);
Console.WriteLine("Sent header");
//Console.WriteLine(BitConverter.ToString(hdr.ToBytes()));

usbif.Read(packet.GetBuffer(), 0, out int readSize2);
Console.WriteLine("Read response");
packet.ParseHeader();

/* Print out beacon packet. */
Console.WriteLine(System.Text.Encoding.ASCII.GetString(packet.GetBuffer()[0x20..(0x20+packet.dataSize)]));

pktMgr.StartThreads();

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
