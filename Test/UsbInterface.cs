namespace Test;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

public class UsbInterface
{
    private IUsbDevice device;
    private UsbEndpointWriter writer;
    private UsbEndpointReader reader;

    public UsbInterface(IUsbDevice dev)
    {
        this.device = dev;

        device.ClaimInterface(device.Configs[0].Interfaces[0].Number);

        writer = device.OpenEndpointWriter(WriteEndpointID.Ep01);
        reader = device.OpenEndpointReader(ReadEndpointID.Ep01);
    }

    public void Read(byte[] buf, int timeout, out int readSize)
    {
        reader.Read(buf, timeout, out readSize);
    }

    public void Read(byte[] buf, int offs, int size, int timeout, out int readSize)
    {
        reader.Read(buf, offs, size, timeout, out readSize);
    }

    public void Write(byte[] buf, int offs, int size, int timeout, out int writeSize)
    {
        writer.Write(buf, offs, size, timeout, out writeSize);
    }

    public void Write(byte[] buf, int timeout, out int writeSize)
    {
        writer.Write(buf, timeout, out writeSize);
    }  
}
