using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Test.Services.hio;

public class HostFilesystemManager : IDisposable {
    private enum DescriptorType
    {
        None,
        File,
        Directory
    }
    
    class FsItem : IDisposable
    {
        protected int fd;
        
        readonly public DescriptorType type;
        
        readonly public string path;
        
        public FsItem(int fd, DescriptorType t, string p)
        {
            this.type = t;
            this.path = p;
        }

        public virtual void Dispose() {}
    }
    
    class FileItem : FsItem
    {
        private readonly FileStream accessor;
        private FileAccess amode;
        
        // guess mode is FsOpenMode?
        public FileItem(int fd, string p, UInt32 mode) : base(fd, DescriptorType.File, p) {
            /* Determine the access mode. */
            int amodei = 0;
            if ((mode & 1) != 0)
                amodei |= (int)FileAccess.Read;
            if ((mode & 2) != 0)
                amodei |= (int)FileAccess.Write;
            this.amode = (FileAccess)amodei;

            /* Open the file. */
            try {
                this.accessor = File.Open(p, FileMode.Open, amode, FileShare.ReadWrite);
            }
            catch (FileNotFoundException)
            {
                throw new HioException(HioErrorCode.PathNotFound);
            }
            catch (DirectoryNotFoundException)
            {
                throw new HioException(HioErrorCode.PathNotFound);
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
                throw new HioException(HioErrorCode.TargetLocked);
            }
        }

        public override void Dispose()
        {
            Close();
        }
        
        public void Close()
        {
            /* Destroy the stream. */
            this.accessor.Dispose();
        }

        public async Task ReadFileAsync(byte[] buf, Int64 fileOffs, int bufOffs, int readSize, CancellationToken ct)
        {
            /* Cannot read past EoF. */
            if (fileOffs + readSize > this.GetFileSize())
                throw new HioException(HioErrorCode.OutOfRange);
            
            /* Can't read a write-only file. */
            if (this.amode != FileAccess.Read && this.amode != FileAccess.ReadWrite)
                throw new HioException(HioErrorCode.TargetLocked); // idk??
            
            try
            {
                this.accessor.Seek(fileOffs, SeekOrigin.Begin);
                await this.accessor.ReadExactlyAsync(buf, bufOffs, readSize, ct);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new HioException(HioErrorCode.OutOfRange);
            }
            catch (ObjectDisposedException)
            {
                throw new HioException(HioErrorCode.Unknown);
            }
        }
        
        public async Task WriteFileAsync(byte[] buf, Int64 fileOffs, int bufOffs, int readSize, CancellationToken ct)
        {
            /* Can't write a read-only file. */
            if (this.amode != FileAccess.Write && this.amode != FileAccess.ReadWrite)
                throw new HioException(HioErrorCode.TargetLocked); // idk??

            try
            {
                this.accessor.Seek(fileOffs, SeekOrigin.Begin);
                await this.accessor.WriteAsync(buf, bufOffs, readSize, ct);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new HioException(HioErrorCode.OutOfRange);
            }
            catch (ObjectDisposedException)
            {
                throw new HioException(HioErrorCode.Unknown);
            }
        }
        
        public Int64 GetFileSize() => this.accessor.Length;
        
        public void SetFileSize(Int64 size) => this.accessor.SetLength(size);
        
        public Task FlushFileAsync(CancellationToken ct) => accessor.FlushAsync(ct);
        
        public void SetPriorityForFile(Int32 prio) {
            /* TODO: What is this? */
        }
        
        public Int32 GetPriorityForFile() {
            /* TODO: What is this? */
            return 0;
        }
    }
    
    // this doesn't hold a handle on directories which might have
    // some implications on when / whether a directory can be deleted
    // or w/e. idk if c# has a cross platform way of holding a dir handle
    class DirectoryItem : FsItem
    {
        private readonly bool readFileInfo = false;
        private readonly bool readDirInfo = false;
        private readonly bool noFileSizes = false;
        
        // Assuming mode is fs DirOpenMode?
        public DirectoryItem(int fd, string path, UInt32 mode) : base(fd, DescriptorType.Directory, path)
        {
            this.readDirInfo  = (mode & 1) != 0;
            this.readFileInfo = (mode & 2) != 0;
            this.noFileSizes  = (mode & 4) != 0;
        }

        public void Close()
        {
            /* idt anything needs to be done? */
        }
        
        public Int64 GetDirectoryEntryCount()
        {
            // TODO: is there a more efficient way of doing this?
            Int64 count = 0;
            
            /* Add files. */
            if (this.readFileInfo)
                count += Directory.GetFiles(this.path).Length;
            if (this.readDirInfo)
                count += Directory.GetDirectories(this.path).Length;

            return count;
        }
        
        public LinkedList<HioDirectoryEntry> ReadDirectory(int maxCount)
        {
            LinkedList<HioDirectoryEntry> entries = new();
            
            if (maxCount <= 0)
                return entries;

            /* Iterate over directories. */
            int count = 0;
            if (this.readDirInfo)
            {
                foreach (var dirName in Directory.EnumerateDirectories(this.path))
                {
                    /* Check if we've read the requested amount. */
                    if (count == maxCount)
                        break;
                    
                    /* FS wants raw file / dir names, not paths. */
                    var info = new DirectoryInfo(dirName);
                    entries.AddLast(new HioDirectoryEntry(
                        HioDirectoryEntryType.Directory,
                        info.Name,
                        info.GetFileSystemInfos().Length
                    ));
                    
                    /* Increment count. */
                    count++;
                }
            }
            
            /* Iterate over files. */
            if (this.readFileInfo && count != maxCount)
            {
                foreach (var fileName in Directory.EnumerateFiles(this.path))
                {
                    /* Check if we've read the requested amount. */
                    if (count == maxCount)
                        break;

                    var info = new FileInfo(fileName);
                    
                    /* Zero file size if requested. */
                    Int64 size = this.noFileSizes ? 0 : info.Length;
                    
                    entries.AddLast(new HioDirectoryEntry(
                        HioDirectoryEntryType.File,
                        info.Name,
                        size
                    ));
                    
                    /* Increment count. */
                    count++;
                }
            }
                
            return entries;
        }
        
        public void SetPriorityForDirectory(Int32 prio) {
            /* TODO: What is this? */
        }
        
        public Int32 GetPriorityForDirectory()
        {
            /* TODO: What his this? */
            return 0;
        }
        
    }

    // literally just making up some number here
    public const int OpenFileCountMax = 256;
    
    private FileDescriptorManager<FsItem> fsItems = new(OpenFileCountMax);
    private static readonly Regex driveRegex = new Regex("^/[aA-zZ]:/");
    private static readonly Regex driveRegex2 = new Regex("^[aA-zZ]:/");
    
    private FileItem GetFileItemOrThrowNotFound(int fd)
    {
        if (fd < OpenFileCountMax && this.fsItems[fd] is FsItem f && f.type == DescriptorType.File)
            return (FileItem)f;

        throw new HioException(HioErrorCode.InvalidFileDescriptor);
    }
    
    private DirectoryItem GetDirectoryItemOrThrowNotFound(int fd)
    {
        if (fd < OpenFileCountMax && this.fsItems[fd] is FsItem f && f.type == DescriptorType.Directory)
            return (DirectoryItem)f;

        throw new HioException(HioErrorCode.InvalidFileDescriptor);
    }

    private static bool ItemExists(string path) => FileExists(path) || DirectoryExists(path);

    private static string GetRealPath(string path)
    {
        /* Linux doesn't like backslashes in paths (based). */
        path = path.Replace('\\', '/');

        /*
         * On linux, pretend that all drives point at root.
         * I might try doing some drive emulation bs at some point
         * since linux and win have diff directory structures. But idk.
         */
        if (driveRegex.IsMatch(path))
        {
            return path.Substring(3);
        }
        else if (driveRegex2.IsMatch(path))
        {
            return path.Substring(2);
        }
        return path;
    }
    
    public HostFilesystemManager() {}

    public void Dispose() => this.CloseAll();

    public void CloseAll()
    {
        /* Close all used file descriptors. */
        var used = this.fsItems.GetUsedDescriptors();
        foreach (int fd in used)
        {
            Debug.Assert(this.fsItems[fd] != null);
            this.fsItems[fd].Dispose();
            this.fsItems[fd] = null;
        }
    }
    
    public Int64 OpenFile(string path, UInt32 mode) {
        /* Register the file. */
        int fd = this.fsItems.AllocateFileDescriptor();
        if (fd < 0)
            throw new HioException(HioErrorCode.AllocationFailed);

        /* Try to open the file. */
#if HIO_TASK_DEBUG
        Console.WriteLine("[hio] OpenFile: {0}, fd={1}", path, fd);
#endif
        var file = new FileItem(fd, GetRealPath(path), mode);
        this.fsItems[fd] = file;
        
        return fd;
    }
    
    public void CloseFile(int fd)
    {
#if HIO_TASK_DEBUG
        Console.WriteLine("[hio] Closing fd={0}", fd);
#endif
        /* Close the file. */
        this.GetFileItemOrThrowNotFound(fd).Close();
        
        /* Remove from the descriptor list. */
        this.fsItems[fd] = null;
    }
    
    public static void CreateFile(string path, Int64 size)
    {
        path = GetRealPath(path);
        Console.WriteLine(path);
        if (ItemExists(path))
            throw new HioException(HioErrorCode.PathAlreadyExists);
        
        if (size < 0)
            throw new HioException(HioErrorCode.OutOfRange);

        try {
            using (var f = File.Create(path))
            {
                f.SetLength(size);
            }
        }
        catch (IOException e)
        {
            /* This seems to get raised if the parent dir doesn't exist? */
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                throw new HioException(HioErrorCode.PathNotFound);
            Console.WriteLine(e);
            Console.WriteLine(path);
            throw new HioException(HioErrorCode.TargetLocked);
        }
        catch (Exception)
        {
            throw new HioException(HioErrorCode.PathNotFound);
        }
    }
    
    public static bool FileExists(string path) {
        try {
            return File.Exists(GetRealPath(path));
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public static void DeleteFile(string path) {
        path = GetRealPath(path);
        if (!FileExists(path))
            throw new HioException(HioErrorCode.PathNotFound);
        
        try {
            File.Delete(path);
        }
        catch (IOException e)
        {
            if (e is DirectoryNotFoundException || e is NotSupportedException || e is PathTooLongException)
                throw new HioException(HioErrorCode.PathNotFound); 
            throw new HioException(HioErrorCode.TargetLocked);
        }
        catch (Exception)
        {
            throw new HioException(HioErrorCode.PathNotFound);
        }
    }
    
    public static void RenameFile(string src, string dst) {
        src = GetRealPath(src);
        dst = GetRealPath(dst);
        try
        {
            File.Move(src, dst);
        }
        catch (IOException e)
        {
            if (e is FileNotFoundException || e is PathTooLongException || e is DirectoryNotFoundException || e is NotSupportedException)
                throw new HioException(HioErrorCode.PathNotFound);
            if (ItemExists(dst))
                throw new HioException(HioErrorCode.PathAlreadyExists);
            
            throw new HioException(HioErrorCode.TargetLocked);
        }
        catch (Exception)
        {
            throw new HioException(HioErrorCode.PathNotFound);
        }
    }
    
    int k = -8;
    public static Int32 GetIOType(string path) {
        path = GetRealPath(path);
        if (Directory.Exists(path))
            return 0;
        if (File.Exists(path))
            return 1;
        return -1;
    }
    
    public static FileTimeStamp GetFileTimeStamp(string path) {
        // TODO: what is the structure of this?
        return new FileTimeStamp(0,0,0);
    }
    
    public Task ReadFileAsync(int fd, byte[] buf, Int64 fileOffs, int bufOffs, int readSize, CancellationToken ct)
    {
        return this.GetFileItemOrThrowNotFound(fd).ReadFileAsync(buf, fileOffs, bufOffs, readSize, ct);
    }
    
    public Task WriteFileAsync(int fd, byte[] buf, Int64 fileOffs, int bufOffs, int readSize, CancellationToken ct)
    {
        return this.GetFileItemOrThrowNotFound(fd).WriteFileAsync(buf, fileOffs, bufOffs, readSize, ct);
    }
    
    public Int64 GetFileSize(int fd)
    {
        return this.GetFileItemOrThrowNotFound(fd).GetFileSize();   
    }
    
    public void SetFileSize(int fd, Int64 size)
    {
        this.GetFileItemOrThrowNotFound(fd).SetFileSize(size);
    }
    
    public Task FlushFileAsync(int fd, CancellationToken ct)
    {
        return this.GetFileItemOrThrowNotFound(fd).FlushFileAsync(ct);
    }
    
    public void SetPriorityForFile(int fd, Int32 prio)
    {
        this.GetFileItemOrThrowNotFound(fd).SetPriorityForFile(prio);   
    }
    
    public Int32 GetPriorityForFile(int fd)
    {
        return this.GetFileItemOrThrowNotFound(fd).GetPriorityForFile();
    }
    
    public int OpenDirectory(string path, UInt32 mode)
    {
        /* Register the directory object. */
        int fd = this.fsItems.AllocateFileDescriptor();
        if (fd < 0)
            throw new HioException(HioErrorCode.AllocationFailed);
        
        /* Open the directory. */
        var dir = new DirectoryItem(fd, GetRealPath(path), mode);
        
#if HIO_TASK_DEBUG
        Console.WriteLine("[hio] OpenDirectory: {0}, fd={1}", path, fd);
#endif

        return fd;
    }
    
    public void CloseDirectory(int fd)
    {
        /* Close the directory. */
        this.GetDirectoryItemOrThrowNotFound(fd).Close();
        
        /* Remove from the descriptor list. */
        this.fsItems[fd] = null;
    }
    
    public static bool DirectoryExists(string path)
    {
        try {
            return Directory.Exists(GetRealPath(path));
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public static void CreateDirectory(string path)
    {
        path = GetRealPath(path);
        if (ItemExists(path))
            throw new HioException(HioErrorCode.PathAlreadyExists);
        try {
            Directory.CreateDirectory(path);
        }
        catch (Exception e)
        {
            throw new HioException(HioErrorCode.PathNotFound);
        }
    }
    
    
    public static void DeleteDirectory(string path, bool recursive)
    {
        path = GetRealPath(path);
        try
        {
            Directory.Delete(path, recursive);
        }
        catch (DirectoryNotFoundException)
        {
            throw new HioException(HioErrorCode.PathNotFound);
        }
        catch (IOException)
        {
            /* Check if the directory is emtpy. */
            if (!recursive && Directory.EnumerateFileSystemEntries(path).GetEnumerator().MoveNext())
                throw new HioException(HioErrorCode.DirectoryNotEmpty);

            throw new HioException(HioErrorCode.TargetLocked);
        }
    }
    
    public static void RenameDirectory(string src, string dst)
    {
        src = GetRealPath(src);
        dst = GetRealPath(dst);
        try
        {
            Directory.Move(src, dst);
        }
        catch (IOException)
        {
            if (ItemExists(dst))
            {
                throw new HioException(HioErrorCode.PathAlreadyExists);
            }
            else if (ItemExists(src))
            {
                throw new HioException(HioErrorCode.PathNotFound);
            }
            throw new HioException(HioErrorCode.TargetLocked);
        }
        catch (Exception)
        {
            throw new HioException(HioErrorCode.PathNotFound);
        }
    }
    
    public Int64 GetDirectoryEntryCount(int fd)
    {
        return this.GetDirectoryItemOrThrowNotFound(fd).GetDirectoryEntryCount();
    }
    
    public LinkedList<HioDirectoryEntry> ReadDirectory(int fd, int maxCount)
    {
        return this.GetDirectoryItemOrThrowNotFound(fd).ReadDirectory(maxCount);
    }
    
    public void SetPriorityForDirectory(int fd, Int32 prio)
    {
        this.GetDirectoryItemOrThrowNotFound(fd).SetPriorityForDirectory(prio);
    }
    
    public Int32 GetPriorityForDirectory(int fd)
    {
        return this.GetDirectoryItemOrThrowNotFound(fd).GetPriorityForDirectory();
    }
    
}