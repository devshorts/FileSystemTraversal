using System;
using System.Collections.Generic;
using System.IO;

namespace FileSystemTraversal
{
    /// <summary>
    /// RealFile is a FileSystemNode that wraps a an acutal file
    /// in the abstract FileSystemNode interface.
    /// </summary>
    public class RealFile : FileSystemNode
    {
        private readonly FileInfo _file;

        internal RealFile(string path) : this(new FileInfo(path)) {}

        public RealFile(FileInfo file)
        {
            _file = file;
        }

        public override IEnumerable<FileSystemNode> Children
        {
            get { return new List<FileSystemNode>(); }
        }

        public override bool IsDirectory
        {
            get { return false; }
        }

        public override bool IsFile
        {
            get { return true; }
        }

        public override Stream InputStream
        {
           get { return _file.OpenRead(); }
        }

        public override string Name
        {
            get { return _file.Name; }
            set { }
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
