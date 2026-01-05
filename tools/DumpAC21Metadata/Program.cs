using System;
using System.IO;
using System.Linq;
using ACadSharp.IO.DWG;

namespace DumpAC21Metadata
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string dwgPath = args.Length > 0
                ? args[0]
                : "samples/sample_AC1021.dwg";

            if (!File.Exists(dwgPath))
            {
                Console.WriteLine($"File not found: {dwgPath}");
                return;
            }

            Console.WriteLine($"=== AC1021 Metadata Dump Tool ===");
            Console.WriteLine($"File: {dwgPath}\n");

            byte[] fileBytes = File.ReadAllBytes(dwgPath);

            // Check version
            string version = System.Text.Encoding.ASCII.GetString(fileBytes, 0, 6);
            Console.WriteLine($"Version: {version}");

            if (version != "AC1021")
            {
                Console.WriteLine("Not an AC1021 file!");
                return;
            }

            // Read encoded header (0x80 to 0x480, 1024 bytes)
            byte[] encodedHeader = new byte[0x400];
            Array.Copy(fileBytes, 0x80, encodedHeader, 0, 0x400);

            Console.WriteLine($"Encoded header size: {encodedHeader.Length} bytes (0x{encodedHeader.Length:X})");

            // Reed-Solomon decode
            byte[] decodedData = new byte[3 * 239];
            ReedSolomonDecode(encodedHeader, decodedData, 3, 239);

            Console.WriteLine($"Decoded header size: {decodedData.Length} bytes (0x{decodedData.Length:X})");

            // Read header info
            long crc = BitConverter.ToInt64(decodedData, 0);
            long unknownKey = BitConverter.ToInt64(decodedData, 8);
            long compressedDataCRC = BitConverter.ToInt64(decodedData, 16);
            int comprLen = BitConverter.ToInt32(decodedData, 24);
            int length2 = BitConverter.ToInt32(decodedData, 28);

            Console.WriteLine($"\n=== Header Info (from decoded RS data) ===");
            Console.WriteLine($"CRC: 0x{crc:X16}");
            Console.WriteLine($"Unknown Key: 0x{unknownKey:X16}");
            Console.WriteLine($"Compressed Data CRC: 0x{compressedDataCRC:X16}");
            Console.WriteLine($"ComprLen: {comprLen} (0x{comprLen:X})");
            Console.WriteLine($"Length2: {length2} (0x{length2:X})");

            // Decompress
            byte[] buffer = new byte[0x110];

            if (comprLen < 0)
            {
                int absLen = -comprLen;
                Console.WriteLine($"\nData is NOT compressed, copying {absLen} bytes");
                Array.Copy(decodedData, 32, buffer, 0, Math.Min(absLen, buffer.Length));
            }
            else
            {
                Console.WriteLine($"\nData IS compressed, decompressing with LZ77...");
                DwgLZ77AC21Decompressor.Decompress(decodedData, 32U, (uint)comprLen, buffer);
            }

            // Count non-zero bytes
            int nonZeroCount = buffer.Count(b => b != 0);
            Console.WriteLine($"\nDecompressed {nonZeroCount} non-zero bytes out of {buffer.Length}");

            // Print first 64 bytes
            Console.WriteLine($"\nFirst 64 bytes:");
            for (int i = 0; i < 64; i += 16)
            {
                Console.Write($"  {i:X4}: ");
                for (int j = 0; j < 16 && i + j < 64; j++)
                {
                    Console.Write($"{buffer[i + j]:X2} ");
                }
                Console.WriteLine();
            }

            // Parse metadata fields
            Console.WriteLine($"\n=== Metadata Fields (Little-Endian u64) ===\n");

            var fields = new[]
            {
                (0x00, "header_size"),
                (0x08, "file_size"),
                (0x10, "pages_map_crc_compressed"),
                (0x18, "pages_map_correction_factor"),
                (0x20, "pages_map_crc_seed"),
                (0x28, "map2_offset"),
                (0x30, "map2_id"),
                (0x38, "pages_map_offset"),
                (0x40, "pages_map_id"),
                (0x48, "header2_offset"),
            };

            foreach (var (offset, name) in fields)
            {
                if (offset + 8 <= buffer.Length)
                {
                    ulong value = BitConverter.ToUInt64(buffer, offset);
                    Console.WriteLine($"0x{offset:X3}: {name,-40} = 0x{value:X16} ({value})");
                }
            }

            // Export full buffer as hex for Rust comparison
            Console.WriteLine($"\n=== Full Buffer (hex) ===");
            string hexOutput = BitConverter.ToString(buffer).Replace("-", "");
            Console.WriteLine(hexOutput);
        }

        private static void ReedSolomonDecode(byte[] encoded, byte[] buffer, int factor, int blockSize)
        {
            int index = 0;
            int n = 0;
            int length = buffer.Length;

            for (int i = 0; i < factor; ++i)
            {
                int cindex = n;
                if (n < encoded.Length)
                {
                    int size = Math.Min(length, blockSize);
                    length -= size;
                    int offset = index + size;

                    while (index < offset)
                    {
                        if (cindex < encoded.Length)
                        {
                            buffer[index] = encoded[cindex];
                        }
                        ++index;
                        cindex += factor;
                    }
                }
                ++n;
            }
        }
    }
}
