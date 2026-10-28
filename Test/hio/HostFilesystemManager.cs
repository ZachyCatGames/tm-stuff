

using System.Data.SqlTypes;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Test.htcs;

namespace Test.hio;

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
            amode = (FileAccess)amodei;

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
#if HTCS_TASK_DEBUG
            Console.WriteLine("[htcs] Closing fd={0}", fd);
#endif
            
            Close();
        }
        
        public void Close()
        {
            /* Destroy the stream. */
            accessor.Dispose();
        }

        public async Task ReadFileAsync(byte[] buf, Int64 fileOffs, int bufOffs, int readSize, CancellationToken ct)
        {
            /* Cannot read past EoF. */
            if (fileOffs + readSize > this.GetFileSize())
                throw new HioException(HioErrorCode.OutOfRange);
            
            /* Can't read a write-only file. */
            if (amode != FileAccess.Read && amode != FileAccess.ReadWrite)
                throw new HioException(HioErrorCode.TargetLocked); // idk??
            
            try
            {
                accessor.Seek(fileOffs, SeekOrigin.Begin);
                await accessor.ReadExactlyAsync(buf, bufOffs, readSize, ct);
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
            if (amode != FileAccess.Write && amode != FileAccess.ReadWrite)
                throw new HioException(HioErrorCode.TargetLocked); // idk??

            try
            {
                accessor.Seek(fileOffs, SeekOrigin.Begin);
                await accessor.WriteAsync(buf, bufOffs, readSize, ct);
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
        
        public Int64 GetFileSize() {
            return accessor.Length;
        }
        
        public void SetFileSize(Int64 size) {
            accessor.SetLength(size);
        }
        
        public Task FlushFileAsync(CancellationToken ct) {
            return accessor.FlushAsync(ct);
        }
        
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
            readDirInfo  = (mode & 1) != 0;
            readFileInfo = (mode & 2) != 0;
            noFileSizes  = (mode & 4) != 0;
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
            if (readFileInfo)
                count += Directory.GetFiles(path).Length;
            if (readDirInfo)
                count += Directory.GetDirectories(path).Length;

            return count;
        }
        
        public LinkedList<HioDirectoryEntry> ReadDirectory(int maxCount)
        {
            LinkedList<HioDirectoryEntry> entries = new();
            
            if (maxCount <= 0)
                return entries;

            /* Iterate over directories. */
            int count = 0;
            if (readDirInfo)
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
            if (readFileInfo && count != maxCount)
            {
                foreach (var fileName in Directory.EnumerateFiles(this.path))
                {
                    /* Check if we've read the requested amount. */
                    if (count == maxCount)
                        break;

                    var info = new FileInfo(fileName);
                    
                    /* Zero file size if requested. */
                    Int64 size = noFileSizes ? 0 : info.Length;
                    
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
    private Regex driveRegex = new Regex("^/[aA-zZ]:/");
    private Regex driveRegex2 = new Regex("^[aA-zZ]:/");
    
    FileItem GetFileItemOrThrowNotFound(int fd)
    {
        if (fd < OpenFileCountMax && fsItems[fd] is FsItem f && f.type == DescriptorType.File)
            return (FileItem)f;

        throw new HioException(HioErrorCode.InvalidFileDescriptor);
    }
    
    DirectoryItem GetDirectoryItemOrThrowNotFound(int fd)
    {
        if (fd < OpenFileCountMax && fsItems[fd] is FsItem f && f.type == DescriptorType.Directory)
            return (DirectoryItem)f;

        throw new HioException(HioErrorCode.InvalidFileDescriptor);
    }

    bool ItemExists(string path)
    {
        return FileExists(path) || DirectoryExists(path);
    }

    string GetRealPath(string path)
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

    public void Dispose()
    {
        CloseAll();
    }

    public void CloseAll()
    {
        /* Close all used file descriptors. */
        var used = fsItems.GetUsedDescriptors();
        foreach (int fd in used)
        {
            Debug.Assert(fsItems[fd] != null);
            fsItems[fd].Dispose();
            fsItems[fd] = null;
        }
    }
    
    public Int64 OpenFile(string path, UInt32 mode) {
        /* Register the file. */
        int fd = fsItems.AllocateFileDescriptor();
        if (fd < 0)
            throw new HioException(HioErrorCode.AllocationFailed);

        /* Try to open the file. */
#if HIO_TASK_DEBUG
        Console.WriteLine("[hio] OpenFile: {0}, fd={1}", path, fd);
#endif
        var file = new FileItem(fd, GetRealPath(path), mode);
        fsItems[fd] = file;
        
        return fd;
    }
    
    public void CloseFile(int fd)
    {
#if HIO_TASK_DEBUG
        Console.WriteLine("[hio] Closing fd={0}", fd);
#endif
        /* Close the file. */
        GetFileItemOrThrowNotFound(fd).Close();
        
        /* Remove from the descriptor list. */
        fsItems[fd] = null;
    }
    
    public void CreateFile(string path, Int64 size)
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
    
    public bool FileExists(string path) {
        try {
            return File.Exists(GetRealPath(path));
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public void DeleteFile(string path) {
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
    
    public void RenameFile(string src, string dst) {
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
    public Int32 GetIOType(string path) {
        path = GetRealPath(path);
        if (Directory.Exists(path))
            return 0;
        if (File.Exists(path))
            return 1;
        return -1;
    }
    
    public FileTimeStamp GetFileTimeStamp(string path) {
        // TODO: what is the structure of this?
        return new FileTimeStamp(0,0,0);
    }
    
    public Task ReadFileAsync(int fd, byte[] buf, Int64 fileOffs, int bufOffs, int readSize, CancellationToken ct)
    {
        return GetFileItemOrThrowNotFound(fd).ReadFileAsync(buf, fileOffs, bufOffs, readSize, ct);
    }
    
    public Task WriteFileAsync(int fd, byte[] buf, Int64 fileOffs, int bufOffs, int readSize, CancellationToken ct)
    {
        return GetFileItemOrThrowNotFound(fd).WriteFileAsync(buf, fileOffs, bufOffs, readSize, ct);
    }
    
    public Int64 GetFileSize(int fd)
    {
        return GetFileItemOrThrowNotFound(fd).GetFileSize();   
    }
    
    public void SetFileSize(int fd, Int64 size)
    {
        GetFileItemOrThrowNotFound(fd).SetFileSize(size);
    }
    
    public Task FlushFileAsync(int fd, CancellationToken ct)
    {
        return GetFileItemOrThrowNotFound(fd).FlushFileAsync(ct);
    }
    
    public void SetPriorityForFile(int fd, Int32 prio)
    {
        GetFileItemOrThrowNotFound(fd).SetPriorityForFile(prio);   
    }
    
    public Int32 GetPriorityForFile(int fd)
    {
        return GetFileItemOrThrowNotFound(fd).GetPriorityForFile();
    }
    
    public int OpenDirectory(string path, UInt32 mode)
    {
        /* Register the directory object. */
        int fd = fsItems.AllocateFileDescriptor();
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
        GetDirectoryItemOrThrowNotFound(fd).Close();
        
        /* Remove from the descriptor list. */
        fsItems[fd] = null;
    }
    
    public bool DirectoryExists(string path)
    {
        try {
            return Directory.Exists(GetRealPath(path));
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public void CreateDirectory(string path)
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
    
    
    public void DeleteDirectory(string path, bool recursive)
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
    
    public void RenameDirectory(string src, string dst)
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
        return GetDirectoryItemOrThrowNotFound(fd).GetDirectoryEntryCount();
    }
    
    public LinkedList<HioDirectoryEntry> ReadDirectory(int fd, int maxCount)
    {
        return GetDirectoryItemOrThrowNotFound(fd).ReadDirectory(maxCount);
    }
    
    public void SetPriorityForDirectory(int fd, Int32 prio)
    {
        GetDirectoryItemOrThrowNotFound(fd).SetPriorityForDirectory(prio);
    }
    
    public Int32 GetPriorityForDirectory(int fd)
    {
        return GetDirectoryItemOrThrowNotFound(fd).GetPriorityForDirectory();
    }
    
}