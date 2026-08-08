namespace Test;

public class FileDescriptorManager<T> {
    int curFileDescriptor;
    readonly T?[] objects;
    
    public readonly int MaxObjectCount;
    
    public FileDescriptorManager(int count)
    {
        this.MaxObjectCount = count;
        this.objects = new T[count];
    }
    
    public int AllocateFileDescriptor()
    {
        for (int i = curFileDescriptor + 1; i != curFileDescriptor; i++)
        {
            if (i == MaxObjectCount)
            {
                i = 1;
            }

            if (objects[i] == null)
            {
                curFileDescriptor = i;
                return i;
            }
        }

        return -1;
    }
    
    public T? FindIf(Predicate<T> pred) {
        foreach (var obj in objects)
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
        get => objects[i];
        set => objects[i] = value;
    }
}