using ACadSharp;
using ACadSharp.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ClassesExporter
{
    class DxfClassExport
    {
        public short ClassNumber { get; set; }
        public string DxfName { get; set; }
        public string CppClassName { get; set; }
        public string ApplicationName { get; set; }
        public short ProxyFlags { get; set; }
        public bool IsEntity { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: ClassesExporter <dwg_file> [output_json]");
                Environment.Exit(1);
            }

            string dwgPath = args[0];
            string outputPath = args.Length >= 2 
                ? args[1] 
                : Path.ChangeExtension(dwgPath, ".acad.classes.json");

            Console.WriteLine($"Loading DWG file: {dwgPath}");
            
            using (DwgReader reader = new DwgReader(dwgPath))
            {
                CadDocument doc = reader.Read();
                
                var classes = doc.Classes
                    .Select(c => new DxfClassExport
                    {
                        ClassNumber = c.ClassNumber,
                        DxfName = c.DxfName,
                        CppClassName = c.CppClassName,
                        ApplicationName = c.ApplicationName,
                        ProxyFlags = (short)c.ProxyFlags,
                        IsEntity = c.IsAnEntity
                    })
                    .OrderBy(c => c.ClassNumber)
                    .ToList();

                Console.WriteLine($"Found {classes.Count} classes");

                var output = new
                {
                    version = doc.Header.Version.ToString(),
                    class_count = classes.Count,
                    classes = classes
                };

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                string json = JsonSerializer.Serialize(output, options);
                File.WriteAllText(outputPath, json);
                
                Console.WriteLine($"Exported {classes.Count} classes to: {outputPath}");

                // Print first few classes for verification
                Console.WriteLine("\nFirst 3 classes:");
                foreach (var cls in classes.Take(3))
                {
                    Console.WriteLine($"  [{cls.ClassNumber}] {cls.DxfName} ({cls.CppClassName})");
                }
            }
        }
    }
}
