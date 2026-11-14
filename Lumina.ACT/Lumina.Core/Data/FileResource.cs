using System;
using System.IO;
using System.Text;

namespace Lumina.Data
{
    public class FileResource
    {
        public FileResource()
        {
        }

        public LuminaFileInfo FileInfo { get; internal set; }
        public byte[] Data { get; internal set; }
        public Span<byte> DataSpan => Data.AsSpan();

        [Obsolete("Use \"Reader.BaseStream\" instead.")]
        public MemoryStream FileStream => Reader.BaseStream as MemoryStream;
        public LuminaBinaryReader Reader { get; internal set; }
        public ParsedFilePath FilePath { get; internal set; }

        public virtual void LoadFile()
        {
        }

        public virtual void SaveFile(string path)
        {
            File.WriteAllBytes(path, Data);
        }

        public void SaveFileRaw(string path)
        {
            File.WriteAllBytes(path, Data);
        }

        public string GetFileHash()
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(Data);
                var sb = new StringBuilder();
                foreach (var b in hash)
                {
                    sb.Append($"{b:x2}");
                }

                return sb.ToString();
            }
        }
    }
}
