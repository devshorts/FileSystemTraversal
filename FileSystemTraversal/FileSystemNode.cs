using System;
using System.Collections.Generic;
using System.IO;

namespace FileSystemTraversal
{
    public class FileSystemNodePathComparitor : IEqualityComparer<FileSystemNode>
    {
        public bool Equals(FileSystemNode x, FileSystemNode y)
        {
            if (x != y)
            {
                return x.Name == y.Name;
            }
            return x == y;
        }

        public int GetHashCode(FileSystemNode obj)
        {
            return obj.Name.GetHashCode();
        }
    }
    /// <summary>
    /// This is an abstract class representing some node somewhere in a filesystem.
    /// A FileSystemNode can be either a directory or a file. Files have an input
    /// stream for reading them. Because of its abstract nature, a FileSystemNode
    /// can be used to read from a container format (.zip, .7z) as if it were a
    /// directory.
    /// </summary>
    public abstract class FileSystemNode : IDisposable
    {
        public abstract IEnumerable<FileSystemNode> Children { get; }
        public abstract bool IsDirectory { get; }
        public abstract bool IsFile { get; }
        public abstract Stream InputStream { get; }
        public abstract string Name { get; set; }
        public abstract FileSystemNode HasFile(string name);
        
        public abstract FileSystemNode GetFile(string path);
        public abstract IEnumerable<FileSystemNode> GetFiles(string directory, SearchOption directoryLevel = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Attempts to translate a file-base node into a container-based node
        /// by looking at the contents/name of the file. 
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        protected static FileSystemNode ResolvePossibleContainer(FileSystemNode node)
        {
            try
            {
                if (node.IsDirectory)
                {
                    // Node is already a container
                    return node;
                }
                if (node.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return new ZipContainer(node);
                }
                if (node.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    return new ZipContainer(node);
                }
                // nothing special about this node
                return node;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception - " + e.Message);
                return node;
            }
        }

        /// <summary>
        /// Creates a new FileSystemNode from a path. If the provided path is a known
        /// container format, a container will be returned.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static FileSystemNode Create(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            if((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                return new RealDirectory(path);
            }
            return ResolvePossibleContainer(new RealFile(path));
        }

        public override string ToString()
        {
            return Name;
        }

        public virtual void Dispose()
        {
            
        }
    }
}
