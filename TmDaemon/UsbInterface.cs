namespace Test;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

public class UsbInterface : IDisposable
{
    private UsbDevice device;
    private UsbEndpointWriter writer;
    private UsbEndpointReader reader;

    private int timeout;
    private bool canceled;
    private Lock mutex;

    public UsbInterface(UsbDevice dev)
    {
        this.device = dev;

        device.ClaimInterface(device.Configs[0].Interfaces[0].Number);

        writer = device.OpenEndpointWriter(WriteEndpointID.Ep01);
        reader = device.OpenEndpointReader(ReadEndpointID.Ep01);

        timeout  = 1000;
        canceled = false;
        mutex    = new Lock();
    }

    public void Dispose()
    {
        device.Close();
        device.Dispose();
    }

    public void SetTimeout(int timeout)
    {
        this.timeout = timeout;
    }

    public void Cancel()
    {
        using (var scope = mutex.EnterScope())
        {
            canceled = true;
        }
    }

    public Error Read(byte[] buf, out int readSize)
    {
        return Read(buf, 0, buf.Length, out readSize);
    }

    public Error Read(byte[] buf, int offs, int size, out int readSize)
    {
        while (true)
        {
            Error result = reader.Read(buf, offs, size, timeout, out readSize);
            if (result != Error.Timeout)
            {
                return result;
            }

            using (var scope = mutex.EnterScope())
            {
                if (canceled)
                {
                    return result; 
                }
            }
        }
    }

    public Error Write(byte[] buf, int offs, int size, out int writeSize)
    {
        while (true)
        {
            Error result = writer.Write(buf, offs, size, timeout, out writeSize);
            if (result != Error.Timeout)
            {
                return result;
            }

            using (var scope = mutex.EnterScope())
            {
                if (canceled)
                {
                    return result; 
                }
            }
        }
    }

    public Error Write(byte[] buf, out int writeSize)
    {
        return Write(buf, 0, buf.Length, out writeSize);
    }

    public int GetUsbDeviceNumber()
    {
        return device.Address | (device.BusNumber << 8);
    }
}
