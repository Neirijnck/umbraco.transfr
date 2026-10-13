using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Neirijnck.Transfr.TestSite.Seeding;

/// <summary>
/// Builds small solid-colour PNG files, so the seeded media library needs no binary files in the repository.
/// </summary>
public static class TestPng
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Solid(byte red, byte green, byte blue, int size = 16)
    {
        // Raw scanlines: filter byte 0 (none) followed by RGB triplets.
        var raw = new byte[size * (1 + (size * 3))];
        for (var row = 0; row < size; row++)
        {
            var offset = row * (1 + (size * 3));
            for (var column = 0; column < size; column++)
            {
                raw[offset + 1 + (column * 3)] = red;
                raw[offset + 2 + (column * 3)] = green;
                raw[offset + 3 + (column * 3)] = blue;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), size);
        header[8] = 8; // bit depth
        header[9] = 2; // colour type: truecolour RGB

        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeAndData = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, typeAndData);
        data.CopyTo(typeAndData, 4);

        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        stream.Write(number);
        stream.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32(typeAndData));
        stream.Write(number);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}