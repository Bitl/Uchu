using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Uchu.Core.IO
{
    public interface IFileResources
    {
        string Root { get; }

        Task<string> ReadTextAsync(string path);

        Task<byte[]> ReadBytesAsync(string path);

        byte[] ReadBytes(string path);

        IEnumerable<string> GetAllFilesWithExtension(string extension);
        
        IEnumerable<string> GetAllFilesWithExtension(string location, string extension);

        Stream GetStream(string path);
    }
}