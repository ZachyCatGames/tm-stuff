using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace Test.Target;

public class UsbInterface : IDisposable
{
    private readonly UsbDevice         device;
    private readonly UsbEndpointWriter writer;
    private readonly UsbEndpointReader reader;
    
    private int  timeout;
    private bool canceled;
    private Lock mutex;

    public LocationId UsbLocationId => device.LocationId;

    public UsbInterface(UsbDevice dev)
    {
        this.device = dev;

        this.device.ClaimInterface(this.device.Configs[0].Interfaces[0].Number);

        this.writer = device.OpenEndpointWriter(WriteEndpointID.Ep01);
        this.reader = device.OpenEndpointReader(ReadEndpointID.Ep01);

        this.timeout  = 1000;
        this.canceled = false;
        this.mutex    = new Lock();
    }

    public void Dispose()
    {
        //device.Close();
        this.device.Dispose();
    }

    public void SetTimeout(int timeout) => this.timeout = timeout;

    public void Cancel()
    {
        this.canceled = true;
    }

    public Error Read(byte[] buf, out int readSize) => this.Read(buf, 0, buf.Length, out readSize);

    public Error Read(byte[] buf, int offs, int size, out int readSize)
    {
        while (true)
        {
            Error result = this.reader.Read(buf, offs, size, timeout, out readSize);
            if (result != Error.Timeout)
            {
                return result;
            }
            
            if (this.canceled)
            {
                return result; 
            }
        }
    }

    public Error Write(byte[] buf, out int writeSize) => this.Write(buf, 0, buf.Length, out writeSize);
    
    public Error Write(byte[] buf, int offs, int size, out int writeSize)
    {
        while (true)
        {
            Error result = this.writer.Write(buf, offs, size, timeout, out writeSize);
            if (result != Error.Timeout)
            {
                return result;
            }
            
            if (this.canceled)
            {
                return result; 
            }
        }
    }
}
