using System;
using System.Collections.Generic;
using System.IO;
using SevenZip;

namespace FileSystemTraversal
{
    /// <summary>
    /// ZipContent treats a record in a .zip file as a file using the
    /// FileSystemNode interface.
    /// </summary>
    public class ZipContent : FileSystemNode
    {
        private readonly SevenZipExtractor _zip;
        public readonly ArchiveFileInfo Entry;

        public ZipContent(SevenZipExtractor zip, ArchiveFileInfo entry)
        {
            _zip = zip;
            Entry = entry;            
        }

        public override IEnumerable<FileSystemNode> Children
        {
            get
            {
                return new List<FileSystemNode>();
            }
        }

        public override bool IsDirectory
        {
            get { return Entry.IsDirectory; }
        }

        public override bool IsFile
        {
            get { return !IsDirectory; }
        }

        private Stream _inputStream;
        public override Stream InputStream
        {
            get
            {
                try
                {
                    if (_inputStream == null)
                    {
                        _inputStream = new MemoryStream();
                        _zip.ExtractFile(Entry.Index, _inputStream);
                        _inputStream.Seek(0, SeekOrigin.Begin);
                    }
                    return _inputStream;
                }
                catch(Exception)
                {
                    return null;
                }
            }
        }

        private string _name;
        public override string Name
        {
            get
            {
               return _name ?? Entry.FileName;
            }
            set { _name = value; }
        }
        

        public override FileSystemNode HasFile(string name)
        {
            throw new NotImplementedException();
        }

        public override FileSystemNode GetFile(string path)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<FileSystemNode> GetFiles(string directory, SearchOption directoryLevel)
        {
            throw new NotImplementedException();
        }
    }
}
