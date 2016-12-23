using System.Threading.Tasks;

namespace BethFile.Archive
{
    public sealed class ExtractedFile
    {
        internal ExtractedFile(string path, Task<byte[]> fileData)
        {
            this.Path = path;
            this.FileData = fileData;
        }

        public string Path { get; }

        public Task<byte[]> FileData { get; }
    }
}
