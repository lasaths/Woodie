using System.Buffers.Binary;
using System.IO.Compression;

namespace Woodie.Rendering;

// Minimal RGBA PNG writer: no GDI+, native image library, or platform-specific bitmap allocations.
internal static class PngEncoder
{
    public static void WriteAtomic(string path, int width, int height, int[] argb, CancellationToken ct)
    {
        string directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = File.Create(temporary)) Write(output, width, height, argb, ct);
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static void Write(Stream output, int width, int height, int[] argb, CancellationToken ct)
    {
        if (width < 1 || height < 1 || argb.Length != checked(width * height)) throw new ArgumentException("Invalid PNG dimensions.");
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; header[9] = 6;
        Chunk(output, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
        {
            byte[] row = new byte[checked(width * 4 + 1)];
            for (int y = 0; y < height; y++)
            {
                ct.ThrowIfCancellationRequested();
                for (int x = 0; x < width; x++)
                {
                    int color = argb[y * width + x], i = 1 + x * 4;
                    row[i] = (byte)(color >> 16); row[i + 1] = (byte)(color >> 8);
                    row[i + 2] = (byte)color; row[i + 3] = (byte)(color >> 24);
                }
                zlib.Write(row);
            }
        }
        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", Array.Empty<byte>());
    }

    static void Chunk(Stream stream, string name, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length); stream.Write(number);
        byte[] type = System.Text.Encoding.ASCII.GetBytes(name); stream.Write(type); stream.Write(data);
        uint crc = 0xffffffff;
        foreach (byte b in type) crc = Crc(crc, b);
        foreach (byte b in data) crc = Crc(crc, b);
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); stream.Write(number);
    }
    static uint Crc(uint crc, byte value)
    {
        crc ^= value;
        for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        return crc;
    }
}
