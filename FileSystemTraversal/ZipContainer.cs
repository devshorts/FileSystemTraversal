using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SevenZip;

namespace FileSystemTraversal
{
    /// <summary>
    /// ZipContainer treats the contents of a .zip file as a directory using the
    /// FileSystemNode interface.
    /// </summary>
    public class ZipContainer : FileSystemNode
    {
        private readonly string _name;
        public SevenZipExtractor Zip { get; set; }
        private readonly ArchiveFileInfo? _entry;
        private ZipContainer _parent;
        private bool _disposed;


        public ZipContainer(string path) : this(new RealFile(path)) { }

        public ZipContainer(FileSystemNode fsn)
        {
            _name = fsn.Name;
            _parent = null;
            Zip = new SevenZipExtractor(fsn.InputStream);
        }

        public ZipContainer(SevenZipExtractor zip, ArchiveFileInfo entry, ZipContainer parent)
        {
            _name = entry.FileName;
            _entry = entry;
            Zip = zip;
            _parent = parent;
        }

        public override IEnumerable<FileSystemNode> Children
        {
            get
            {
                // When reading the .zip's contents, attempt to resolve any zipped file
                // in case it is also a container (ex. nested zip)
                return (Zip.ArchiveFileData.Select(
                    entry => ResolvePossibleContainer(GetContent(entry))));
            }
        }

        public bool FileExists(string path)
        {
            return Zip.ArchiveFileData.Any(item => !item.IsDirectory && item.FileName == path);
        }

        public override FileSystemNode GetFile(string path)
        {
            return
                GetFiles(Path.GetDirectoryName(path)).Where(
                    entry => Path.GetFileName(entry.Name) == Path.GetFileName(path)).FirstOrDefault();
        }

        public override IEnumerable<FileSystemNode> GetFiles(string directory, SearchOption directoryLevel = SearchOption.TopDirectoryOnly)
        {
            bool isRootDir = String.IsNullOrEmpty(directory);


            return Zip.ArchiveFileData
                .Where(entry =>
                           {
                               if(entry.IsDirectory)
                               {
                                   return false;
                               }

                               var zipDirectorySplit = SplitDir(entry.FileName);
                               if (zipDirectorySplit.Count() == 1 && isRootDir &&
                                   directoryLevel == SearchOption.TopDirectoryOnly)
                               {
                                   return true;
                               }

                               if (zipDirectorySplit != null && zipDirectorySplit.Length != 0)
                               {
                                   if (directoryLevel == SearchOption.AllDirectories)
                                   {
                                       if (Path.GetDirectoryName(entry.FileName).Contains(directory))
                                       {
                                           return true;
                                       }
                                   }

                                   if (Path.GetDirectoryName(entry.FileName) == directory)
                                   {
                                       return true;
                                   }
                               }
                               return false;

                           })
                .Select(entry => ResolvePossibleContainer(GetContent(entry)));

        }

        private FileSystemNode GetContent(ArchiveFileInfo entry)
        {
            if(Path.GetExtension(entry.FileName) == ".zip" || Path.GetExtension(entry.FileName) == ".7z")
            {
                // extract the embedded zip and create a new zip container for it with a reference to the parent.
                // if we need to we'll leave the contents un-extracted so we can extract that at will later
                return new ZipContainer(new SevenZipExtractor(ExtractEntry(Zip, entry)), entry, this);
            }

            return new ZipContent(Zip, entry);
        }

        private static string[] SplitDir(string dir)
        {
            return dir.Split(new[] { '/' });
        }
        public override bool IsDirectory
        {
            get { return true; }
        }

        public override bool IsFile
        {
            get { return false; }
        }

        public override Stream InputStream
        {
            get
            {
                // we have a zip within a zip
                if(_entry != null)
                {
                    return ExtractEntry(_parent.Zip, _entry);
                }

                return null;
            }
        }

        private Stream ExtractEntry(SevenZipExtractor zip, ArchiveFileInfo? entry)
        {
            if (entry != null && zip != null)
            {
                var memoryStream = new MemoryStream();

                ArchiveFileInfo entryValue = entry.GetValueOrDefault();

                if (entryValue != null)
                {
                    zip.ExtractFile(entryValue.Index, memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    return memoryStream;
                }
            }
            return null;
        }

        public override string Name
        {
            get { return _name; }
            set {  }
        }

        public override FileSystemNode HasFile(string name)
        {
            throw new NotImplementedException();
        }

        protected void DisposeImpl(bool disposing)
        {
            if(disposing)
            {
                if(_parent != null)
                {
                    _parent.Dispose();
                    _parent = null;
                }
                else if (_parent == null && Zip != null)
                {
                    Zip.Dispose();
                    Zip = null;
                }
            }
        }

        public override void Dispose()
        {
            if (!_disposed)
            {
                DisposeImpl(true);
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
