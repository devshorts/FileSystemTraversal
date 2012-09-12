using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileSystemTraversal
{
    /// <summary>
    /// RealDirectory is a FileSystemNode that wraps a an acutal directory
    /// in the abstract FileSystemNode interface.
    /// </summary>
    public class RealDirectory : FileSystemNode
    {
        private readonly DirectoryInfo _directory;

        internal RealDirectory(string path) : this(new DirectoryInfo(path)) {}

        public RealDirectory(DirectoryInfo directory)
        {
            _directory = directory;
        }

        public override IEnumerable<FileSystemNode> Children
        {
            get
            {
                var dirs = _directory.GetDirectories().Select(di => new RealDirectory(di)).Cast<FileSystemNode>();
                var files = _directory.GetFiles().Select(fi => ResolvePossibleContainer(new RealFile(fi)));
                return dirs.Union(files);
            }
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
            get { throw new NotImplementedException(); }
        }

        public override string Name
        {
            get { return _directory.Name; }
            set {  }
        }

        public override FileSystemNode HasFile(string name)
        {
            // search directories and return if it has the filename
            throw new NotImplementedException();
        }

        public override FileSystemNode GetFile(string path)
        {
            var directorypath = Path.GetDirectoryName(path);
            if(directorypath == _directory.FullName)
            {
                var file = Directory.GetFiles(path).FirstOrDefault();
                return new RealFile(file);
            }
            return null;
        }

        public override IEnumerable<FileSystemNode> GetFiles(string directory, SearchOption directoryLevel)
        {
            return Directory.GetFiles(_directory.FullName, "*", directoryLevel).Select(f => new RealFile(f));
        }

    }
}
