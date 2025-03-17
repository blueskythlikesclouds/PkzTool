using System.Text;

if (args.Length == 0)
{
    Console.WriteLine("Usage: PkzTool [source] [destination]");
    return;
}

args[0] = Path.GetFullPath(args[0]);

List<(string FilePath, string FileName, int FileNameSize, long FileSize)> files = Directory.EnumerateFiles(args[0], "*", SearchOption.AllDirectories).Select(x => 
{
    string fileName = x[(args[0].Length + 1)..].Replace('\\', '/');
    return (x, fileName, Encoding.UTF8.GetByteCount(fileName), new FileInfo(x).Length);
}).ToList();

int stringTableSize = 0x8 + files.Sum(x => (x.FileNameSize + 8) & ~7);
long headerSize = ((1 + files.Count) * 0x20 + stringTableSize + 63) & ~63;
long pkzSize = headerSize + files.Sum(x => (x.FileSize + 63) & ~63);

using var writer = new BinaryWriter(File.Create(args.Length > 1 ? args[1] : args[0] + ".pkz"));

writer.Write(0x200006C7A6B70);
writer.Write(pkzSize);
writer.Write(files.Count);
writer.Write(0x20);
writer.Write(stringTableSize);
writer.Write(0x1000);

int stringTableOffset = 0x8;
long dataOffset = headerSize;

foreach (var (_, _, fileNameSize, fileSize) in files)
{
    writer.Write(stringTableOffset);
    writer.Write(0);
    writer.Write(fileSize);
    writer.Write(dataOffset);
    writer.Write(fileSize);

    stringTableOffset += (fileNameSize + 7) & ~7;
    dataOffset += (fileSize + 63) & ~63;
}

writer.Write(0x656E6F4EL);

Span<byte> buffer = stackalloc byte[(files.Max(x => x.FileNameSize) + 8) & ~7];

foreach (var (_, fileName, fileNameSize, _) in files)
{
    int size = Encoding.UTF8.GetBytes(fileName, buffer);
    buffer[size..].Clear();
    writer.Write(buffer[..((fileNameSize + 8) & ~7)]);
}

while ((writer.BaseStream.Position & 63) != 0)
    writer.Write((byte)0);

foreach (var (filePath, _, _, _) in files)
{
    using var stream = File.OpenRead(filePath);
    stream.CopyTo(writer.BaseStream);

    while ((writer.BaseStream.Position & 63) != 0)
        writer.Write((byte)0);
}