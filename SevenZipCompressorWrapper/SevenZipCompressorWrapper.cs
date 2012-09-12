using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SevenZip;

namespace SevenZipCompressorWrapper
{
    /// <summary>
    /// To Cancel a Compression CommitZipWithArgs must be used with fileCompressionStartProgress event.cancel updated to true.
    /// Cancelation of a FileStream will delete the File on Dispose
    /// 
    /// A compression with OutArchiveFormat.Zip does not trigger any progress even thus use CommitZipWithArgs > fileCompressionStartProgress 
    /// to get progress on a zip file.
    /// </summary>
    public class SevenZipCompressorWrapper : Disposables
    {
        #region Data

        private Stream OutputStream { get; set; }

        private string OutputStreamPath { get; set; }

        private SevenZipCompressor _compressor;

        private Action _compressionAction;

        private Action<ProgressEventArgs> _compressionActionWithArgs;
        private Action<FileNameEventArgs> _compressionActionWithArgsStarted;


        // dictionary of streams that the wrapper will compress
        private readonly Dictionary<string, Stream> _valueDictionary = new Dictionary<string, Stream>();

        // dictionary of stream sizes
        private readonly Dictionary<string, long> _sizeDictionary = new Dictionary<string, long>();

        // if we create any copies of the streams to files, track them here for cleanup later
        private readonly List<string> _dumpedFiles = new List<string>();

        private Boolean _committed;
        private Boolean _canceled;

        private static Boolean _initialized;

        private static object _sevenZipLock = new object();

        #endregion

        #region Constructors

        /// <summary>
        /// Use this constructor when you need to append items to a stream
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="method"></param>
        /// <param name="level"></param>
        /// <param name="format"></param>
        public SevenZipCompressorWrapper(Stream outputStream, CompressionMethod method = CompressionMethod.Default,
                                        CompressionLevel level = CompressionLevel.Normal,
                                        OutArchiveFormat format = OutArchiveFormat.SevenZip)
        {
            if (outputStream is FileStream && Path.GetExtension((outputStream as FileStream).Name) == ".zip" && format == OutArchiveFormat.SevenZip)
            {
                throw new SevenZipException("Attempting to create a 7z file with the extension of zip. Only .7z extensions are allowed: " + (outputStream as FileStream).Name);    
            }

            InitSevenZip();
            OutputStream = outputStream;
            _compressor = new SevenZipCompressor
                              {
                                  CompressionMethod = method, 
                                  CompressionLevel = level,
                                  ArchiveFormat = format
                              };

            SetCustomParameters();
            
        }

        public static void InitSevenZip()
        {
            InitSevenZip(SevenZipDllPaths);
        }

        public static void InitSevenZip(IEnumerable<string> searchPaths)
        {
            lock (_sevenZipLock)
            {
                if (!_initialized)
                {
                    var path = searchPaths.Select(SevenZipPath).Where(File.Exists).FirstOrDefault();
                    SevenZipBase.SetLibraryPath(path);
                    _initialized = true;
                }
            }
        }

        private static IEnumerable<string> SevenZipDllPaths
        {
            get
            {
                var paths = new[]
                                {
                                    ".",
                                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                                    @"C:\Cube\wwwroot\bin",
                                    @"C:\AutoUpdateServer.Web\bin",

                                    // i can't use projectPath since its in test utils, probably should move it
                                    // so i can get rid of this horrible statement
                                    @"C:\Projects\cube\trunk\src\AtellisShared\AtellisShared.common\bin\debug",
                                    @"C:\Projects\cube\trunk\src\AtellisShared\AtellisShared.common\bin\release"
                                }.Concat(IoUtil.DefaultSearchPaths.Select(x=>Path.Combine(x,"bin"))).ToArray();
                return paths;
            }
        }

        private static string SevenZipPath(string path)
        {
            return Path.Combine(path, "7z.dll");
        }


        public SevenZipCompressorWrapper(string path, CompressionMethod method = CompressionMethod.Default,
                                        CompressionLevel level = CompressionLevel.Normal,
                                        OutArchiveFormat format = OutArchiveFormat.SevenZip)
            : this(new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None), method, level, format)
        {
            if(Path.GetExtension(path) == ".zip" && format == OutArchiveFormat.SevenZip)
            {
                throw new SevenZipException("Attempting to create a 7z file with the extension of zip. Only .7z extensions are allowed: " + path);
            }
        }

        /// <summary>
        /// Use this constructor when you need to append items to a stream
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="format"></param>
        public SevenZipCompressorWrapper(Stream outputStream, OutArchiveFormat format)
            : this(outputStream, CompressionMethod.Default, CompressionLevel.Normal, format)
        {
           
        }

        public SevenZipCompressorWrapper(string outputPath, OutArchiveFormat format)
            : this(new FileStream(outputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None), format)
        {
        }

        /// <summary>
        /// Use this constructor to access basic compression of files/directories in a single go
        /// </summary>
        public SevenZipCompressorWrapper(OutArchiveFormat format,
                                        CompressionLevel level = CompressionLevel.Normal,
                                        CompressionMethod method = CompressionMethod.Default 
                                        )
        {
            InitSevenZip();
            _compressor = new SevenZipCompressor
            {
                CompressionMethod = method,
                CompressionLevel = level,
                ArchiveFormat = format
            };

            SetCustomParameters();
        }

        /// <summary>
        /// Use this constructor to access basic compression of files/directories in a single go
        /// </summary>
        public SevenZipCompressorWrapper(CompressionMethod method = CompressionMethod.Default,
                                        OutArchiveFormat format = OutArchiveFormat.SevenZip) 
            : this(format, CompressionLevel.Normal, method)
        {
        }

        #endregion 

        #region Dynamic Adding

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">Full path to file</param>
        /// <param name="entryPath">Relative file name in zip</param>
        public void AddFileToZip(string path, string entryPath)
        {
            var stream = new FileInfo(path).Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            AddStreamToDictionary(stream, entryPath);
        }

        public void AddDirectoryToZip(string folder, bool recursive)
        {
            Directory.GetFiles(folder, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .ForEach(file => AddFileToZip(file, file.Replace(folder + @"\", "")));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">Full path to file, full path will also be used as the location in the zip file</param>
        public void AddFileToZip(string path)
        {
            AddFileToZip(path, path);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream">Stream of data to compress</param>
        /// <param name="entryPath">Relative file name in zip</param>
        public void AddStreamToZip(Stream stream, string entryPath)
        {
            AddStreamToDictionary(stream, entryPath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text">Text to add</param>
        /// <param name="entryPath">Relative file name in zip</param>
        public void AddTextToZip(string text, string entryPath)
        {
            byte[] byteArray = Encoding.ASCII.GetBytes(text);
            var stream = new MemoryStream(byteArray);

            AddStreamToDictionary(stream, entryPath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="zip">Zip container with data to add</param>
        /// <param name="entryPath">Relative file name in zip</param>
        public void AddFileSystemNodeToZip(FileSystemNode zip, string entryPath = null)
        {
            if (zip != null && zip.InputStream != null)
            {
                AddStreamToDictionary(zip.InputStream, entryPath ?? zip.Name);
            }
        }

        private void AddStreamToDictionary(Stream stream, string entryPath)
        {
            _valueDictionary[entryPath] = stream;
            _sizeDictionary[entryPath] = stream.Length;
        }

        
        #endregion

        #region Committing

        /// <summary>
        /// Begins compression. Progress ticks can be utilized with the progress Action
        /// </summary>
        /// <param name="progress"></param>
        public void CommitZip(Action progress = null)
        {
            CheckStreamDictionary();

            _compressionAction = progress;
            _compressor.Compressing += CompressingEventHandler;
            _compressor.CompressStreamDictionary(_valueDictionary, OutputStream);
            _committed = true;
        }

        /// <summary>
        /// Begins compression. Use this to get progress details for archive of output type : OutArchiveFormat.Zip
        /// File Progress can be utilized with fileCompressionStartProgress.
        /// Canceling Compression can be done by utilizing the fileCompressionStartProgress.
        /// </summary>
        /// <param name="fileCompressionStartProgress">Allow cancelation of the compression and provides file events progress. </param>
        public void CommitZipWithArgs(Action<FileNameEventArgs> fileCompressionStartProgress = null)
        {
            CommitZipWithArgs(null,fileCompressionStartProgress);
        }

        /// <summary>
        /// Begins compression. 
        /// Progress ticks can be utilized with ProgressEventArg. 
        /// File Progress can be utilized with fileCompressionStartProgress.
        /// Canceling Compression can be done by utilizing the fileCompressionStartProgress.
        /// </summary>
        /// <param name="progress">Provides progress events.Does not work with OutArchiveFormat.Zip </param>
        /// <param name="fileCompressionStartProgress">Allow cancelation of the compression and provides file events progress. </param>
        public void CommitZipWithArgs(Action<ProgressEventArgs> progress = null, Action<FileNameEventArgs> fileCompressionStartProgress = null)
        {
            CheckStreamDictionary();

            _compressionActionWithArgs = progress;
            _compressionActionWithArgsStarted = fileCompressionStartProgress;
            _compressor.Compressing += CompressingEventHandler;
            _compressor.FileCompressionStarted += FileCompressionStartedEventHandler;
            _compressor.CompressStreamDictionary(_valueDictionary, OutputStream);
            _committed = true;
        }

        private void CompressingEventHandler(object sender, ProgressEventArgs e)
        {
            if (_compressionAction != null)
            {
                _compressionAction();
            }
            if (_compressionActionWithArgs != null)
            {
                _compressionActionWithArgs(e);
            }

        }

        private void FileCompressionStartedEventHandler(object sender, FileNameEventArgs e)
        {
            if (_compressionActionWithArgsStarted != null)
            {
                _compressionActionWithArgsStarted(e);
                if (e.Cancel)
                {
                    _canceled = e.Cancel;
                }
            }

        }


        #endregion

        #region Stream Copying

        /// <summary>
        /// Validates that any of the added streams aren't growing. If they are will create a copy
        /// of the stream and replace the current stream with a copy so that the growing stream
        /// doesn't stall the compression (case 57242)
        /// </summary>
        private void CheckStreamDictionary()
        {
            foreach (var entry in _valueDictionary.Keys.ToList())
            {
                var item = new KeyValuePair<string, Stream>(entry, _valueDictionary[entry]);
                if (item.Value.Length > _sizeDictionary[item.Key])
                {
                    CreateCopyAndUpdate(item, _sizeDictionary[item.Key]);
                }
            }
        }

        private void CreateCopyAndUpdate(KeyValuePair<string, Stream> item, long copySize)
        {
            Stream streamCopy = CreateCopy(item, copySize);

            // remove the stream from the dictionary
            
            _valueDictionary.Remove(item.Key);

            item.Value.Dispose();

            if (streamCopy != null)
            {
                _valueDictionary[item.Key] = streamCopy;
            }
        }

        /// <summary>
        /// Copies the input stream at the current size that it is, whether returning a memory copy
        /// if the stream is smaller than 50MB or writing the file to disk and re-streaming
        /// so as to not bog down memory usage
        /// </summary>
        /// <param name="item"></param>
        /// <param name="copySize"></param>
        /// <returns></returns>
        private Stream CreateCopy(KeyValuePair<string, Stream> item, long copySize)
        {
            Stream copy;
            if (item.Value.Length < MaxMemoryCopySize)
            {
                copy = CreateMemoryCopy(item, copySize);
            }
            else
            {
                copy = CreateFileCopy(item, copySize);
            }

            if (copy != null && copy.CanSeek)
            {
                SeekBegin(copy);
                return copy;
            }

            return null;
        }

        private void SeekBegin(Stream stream)
        {
            if(stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
        }

        private Stream CreateFileCopy(KeyValuePair<string, Stream> item, long copySize)
        {
            var tmpFile = IoUtil.GetRandomFileName(KnownPath.Temp.ZipCache, "memoryItem",".tmp");

            Log.Debug(this, "Creating file copy of input stream correlating to zip entry {0} since it's size {1} beyond the max memory copy limit ({2}) and has grown since we added it to the zippable dictionary",
                item.Key, 
                copySize,
                MaxMemoryCopySize);

            try
            {
                using (var fileStream = new FileStream(tmpFile, FileMode.OpenOrCreate))
                {
                    CopyStream(item.Value, fileStream, copySize);
                }
            }
            catch (Exception ex)
            {
                Log.Error(this, "Unable to create file copy of stream that is too large to copy to memory", ex);
            }
            finally
            {
                _dumpedFiles.Add(tmpFile);
            }

            if (File.Exists(tmpFile))
            {
                return File.OpenRead(tmpFile);
            }

            Log.Error(this, "Temp file meant for file copy doesn't exist, not zipping: {0}", tmpFile);
            return null;
        }

        private Stream CreateMemoryCopy(KeyValuePair<string, Stream> item, long copySize)
        {
            var stream = new MemoryStream(Convert.ToInt32(copySize));
            CopyStream(item.Value, stream, copySize);
            return stream;
        }

        private void CopyStream(Stream input, Stream output, long streamSize)
        {
            SeekBegin(input);
            SeekBegin(output);
            IoUtil.CopyStream(input, output, 4096, Convert.ToInt32(streamSize));
        }

        /// <summary>
        /// 50 MB
        /// </summary>
        protected long MaxMemoryCopySize
        {
            get { return 1024 * 1024 * 50; }
        }

        #endregion

        #region Static Compression

        public void CompressDirectory(string directory, string outputZipName)
        {
            _compressor.CompressDirectory(Path.GetFullPath(directory), outputZipName);
            _committed = true;
        }

        public void CompressDirectories(IEnumerable<string> inputFolders, string outputZipFileName)
        {
            var allFiles = inputFolders.SelectMany(Directory.GetFiles);
            _compressor.CompressFiles(outputZipFileName, allFiles.ToArray());
            _committed = true;
        }

        public void CompressFiles(IEnumerable<string> files, string outputZipFileName)
        {
            _compressor.CompressFiles(outputZipFileName, files.Select(Path.GetFullPath).ToArray());
            _committed = true;
        }

        public void CompressStream(Stream inputStream, Stream outputStream)
        {
            _compressor.CompressStream(inputStream, outputStream);
            outputStream.Seek(0, SeekOrigin.Begin);
            _committed = true;
        }

        private void SetCustomParameters()
        {
            _compressor.CustomParameters["mt"] = "on";
        }

        #endregion

        #region Disposables Implementation

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                if(!_committed)
                {
                    CommitZip();
                }

                PreDispose();

                base.Dispose(true);
                CleanUp();

            }
        }

        private void PreDispose()
        {
            var outputFileStream = OutputStream as FileStream;

            if (_canceled && outputFileStream != null)
            {
                OutputStreamPath = outputFileStream.Name;
            }

            // add all the disposable items to the base class collection
            _valueDictionary.Values.ForEach(Add);
            Add(OutputStream);
        }

        private void CleanUp()
        {
            if (!String.IsNullOrEmpty(OutputStreamPath))
            {
                FileUtil.SafeDelete(OutputStreamPath);
            }
            _compressor = null;

            if (!CollectionUtil.IsNullOrEmpty(_dumpedFiles))
            {
                _dumpedFiles.ForEach(file => FileUtil.SafeDelete(file));
            }
        }

        #endregion
    }
}
