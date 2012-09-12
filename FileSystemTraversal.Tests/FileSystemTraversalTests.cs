using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SevenZip;

namespace FileSystemTraversal.Tests
{
    [TestFixture]
    public class FileSystemNodeTest : AssertionHelper
    {
        [TestFixtureSetUp]
        public void Init7z()
        {
            SevenZipExtractor.SetLibraryPath(@"../../../Resources/7z.dll");
        }



        [Test]
        public void TestEmbeddedZips()
        {
            var zipContainer = FileSystemNode.Create(@"../../../Resources/Outer.7z");

            RecurseThroughContainer(zipContainer);
        }

        private void RecurseThroughContainer(FileSystemNode zipContainer, int level = 0)
        {
            foreach (var child in zipContainer.Children)
            {
                var tabs = Enumerable.Repeat("\t", level).Aggregate("", (a,b)=>a+b);
                if (child.IsFile)
                {
                    using (var stream = new StreamReader(child.InputStream))
                    {
                        Console.WriteLine("{0}{1} contents: {2}", tabs, child.Name, stream.ReadToEnd());
                    }
                }
                else
                {
                    Console.WriteLine("{0}file Name in zip {1}", tabs, child.Name);
                }

                RecurseThroughContainer(child, ++level);
            }
        }

        /// <summary>
        /// Copies the contents of input to output. Doesn't close either stream.
        /// </summary>
        public static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, len);
            }
        }
    }
}
