

using System.Data.SqlTypes;
using System.Text;
using Test.htcs;

namespace Test.hio;

public class HostFilesystemManager {
    enum DescriptorType
    {
        None,
        File,
        Directory
    }
    
    class FsItem
    {
        readonly public DescriptorType type;
        
        readonly public string path;
        
        public FsItem(DescriptorType t, string p)
        {
            this.type = t;
            this.path = p;
        }
    }
    
    class FileItem : FsItem
    {
        readonly FileStream accessor;
        readonly bool append;
        
        // guess mode is FsOpenMode?
        public FileItem(string p, UInt32 mode) : base(DescriptorType.File, p) {
            /* Determine the access mode. */
            int amode = 0;
            if ((mode & 1) != 0)
                amode |= (int)FileAccess.Read;
            if ((mode & 2) != 0)
                amode |= (int)FileAccess.Write;
                
            /* Check for append mode. */
            this.append = (mode & 4) != 0;

            /* Open the file. */
            try {
                this.accessor = File.Open(p, FileMode.Open, (FileAccess)amode);
            }
            catch (FileNotFoundException)
            {
                throw new HioException(HioErrorCode.PathNotFound);
            }
            catch (DirectoryNotFoundException)
            {
                throw new HioException(HioErrorCode.PathNotFound);
            }
        }

        public async void ReadFileAsync(byte[] buf, Int64 fileOffs, int bufOffs, int readSize)
        {
            /* Cannot read past EoF. */
            if (fileOffs + readSize > this.GetFileSize())
                throw new HioException(HioErrorCode.OutOfRange);
            
            accessor.Seek(fileOffs, SeekOrigin.Begin);
            await accessor.ReadAsync(buf, bufOffs, readSize);
        }
        
        public async void WriteFileAsync(byte[] buf, Int64 fileOffs, int bufOffs, int readSize)
        {
            /* Cannot write past EoF unless in append mode. */
            if (fileOffs + readSize > this.GetFileSize() && !this.append)
                throw new HioException(HioErrorCode.OutOfRange);
            
            accessor.Seek(fileOffs, SeekOrigin.Begin);
            await accessor.WriteAsync(buf, bufOffs, readSize);
        }
        
        public Int64 GetFileSize() {
            return accessor.Length;
        }
        
        public void SetFileSize(Int64 size) {
            accessor.SetLength(size);
        }
        
        public Task FlushFile() {
            return accessor.FlushAsync();
        }
        
        public void SetPriorityForFile(Int32 prio) {
            /* TODO: What is this? */
        }
        
        public Int32 GetPriorityForFile() {
            /* TODO: What is this? */
            return 0;
        }
    }
    
    class DirectoryItem : FsItem
    {
        public DirectoryItem(string path, UInt32 mode) : base(DescriptorType.Directory, path) {
            
        }
        
        public UInt64 GetDirectoryEntryCount()
        {
            throw new NotImplementedException();
        }
        
        public LinkedList<HioDirectoryEntry> ReadDirectory()
        {
            LinkedList<HioDirectoryEntry> entries = new();

            /* Iterate over directories. */
            foreach (var dirName in Directory.EnumerateDirectories(this.path))
            {
                /* FS wants raw file / dir names, not paths. */
                var info = new DirectoryInfo(dirName);
                entries.AddLast(new HioDirectoryEntry(
                    HioDirectoryEntryType.Directory,
                    info.Name,
                    info.GetFileSystemInfos().Length
                ));
            }
            
            /* Iterate over files. */
            foreach (var fileName in Directory.EnumerateFiles(this.path))
            {
                var info = new FileInfo(fileName);
                entries.AddLast(new HioDirectoryEntry(
                    HioDirectoryEntryType.File,
                    info.Name,
                    info.Length
                ));
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
    
    FileDescriptorManager<FsItem> fsItems = new(OpenFileCountMax);
    
    public HostFilesystemManager() {}
    
    public Int64 OpenFile(string path, UInt32 mode) {
        /* Try to open the file. */
        var file = new FileItem(path, mode);
        
        /* Register the file. */
        return fsItems.RegisterNewT(file);
    }
    
    public bool FileExists(string path) {
        return File.Exists(path);
    }
    
    public void DeleteFile(string path) {
        File.Delete(path);
    }
    
    public void RenameFile(string path1, string path2) {
        // TODO: ordering?
        //File.Move(path1, path2);
        throw new NotImplementedException();
    }
    
    public UInt32 GetIOType(string path) {
        // TODO: what is this / how does fs use it?
        throw new NotImplementedException();
    }
    
    public FileTimeStamp GetFileTimeStamp(string path) {
        // TODO: what is the structure of this?
        return new FileTimeStamp(0,0,0);
    }
    
    
    
}