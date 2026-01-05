using System;
using System.IO;
using System.Linq;
using ACadSharp.IO;

namespace DumpPageMap
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: DumpPageMap <dwg-file-path>");
                return;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return;
            }

            try
            {
                Console.WriteLine($"Reading DWG file: {filePath}");

                // Read the file using ACadSharp's internal reader
                using (var stream = File.OpenRead(filePath))
                {
                    var reader = new ACadSharp.IO.DWG.DwgReader(stream);

                    // Access internal page map data
                    // Note: This requires accessing internal ACadSharp APIs
                    Console.WriteLine("File opened successfully");
                    Console.WriteLine($"Version: {reader.Version}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
