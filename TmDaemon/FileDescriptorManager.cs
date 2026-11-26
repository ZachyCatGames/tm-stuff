namespace Test;

public class FileDescriptorManager<T> {
    private int           curFileDescriptor;
    private readonly T?[] objects;
    
    public readonly int MaxObjectCount;
    
    public FileDescriptorManager(int count)
    {
        this.MaxObjectCount = count;
        this.objects        = new T[count];
        
        for (int i = 0; i < count; i++)
        {
            this.objects[i] = default(T);
        }
    }
    
    public int AllocateFileDescriptor()
    {
        for (int i = this.curFileDescriptor + 1; i != this.curFileDescriptor; i++)
        {
            if (i == this.MaxObjectCount)
            {
                i = 1;
            }

            if (this.objects[i] == null)
            {
                this.curFileDescriptor = i;
                return i;
            }
        }

        return -1;
    }
    
    public int RegisterNewT(T obj) {
        /* Allocate an fd. */
        int fd = this.AllocateFileDescriptor();
        
        /* Assign it to the object. */
        this.objects[fd] = obj;
        
        return fd;
    }
    
    public T? FindIf(Predicate<T> pred) {
        foreach (var obj in this.objects)
        {
            if (obj != null && pred(obj))
            {
                return obj;
            }
        }
        return default(T);
    }
    
    public T? this[int i]
    {
        get => this.objects[i];
        set => this.objects[i] = value;
    }

    public List<int> GetUsedDescriptors()
    {
        var list = new List<int>();
        for (int i = 0; i < this.MaxObjectCount; i++)
        {
            if (this.objects[i] != null)
            {
                list.Add(i);
            }
        }

        return list;
    }

    public void Clear()
    {
        for (int i = 0; i < this.MaxObjectCount; i++)
        {
            this.objects[i] = default(T);
        }
    }
}