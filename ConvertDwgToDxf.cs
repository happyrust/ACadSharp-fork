using ACadSharp;
using ACadSharp.IO;
using System;
using System.IO;

namespace ACadSharp.Converter
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputDwg = "/Volumes/DPC/work/cad-code/ACadSharp/input-files/2416流程图图例-通风.dwg";
            string outputDxf = "/Volumes/DPC/work/cad-code/ACadSharp/input-files/2416流程图图例-通风.dxf";

            Console.WriteLine($"Reading {inputDwg}...");

            try
            {
                CadDocument doc;
                using (DwgReader reader = new DwgReader(inputDwg))
                {
                    doc = reader.Read();
                }

                Console.WriteLine($"Writing {outputDxf}...");
                DxfWriter.Write(outputDxf, doc);

                Console.WriteLine("Conversion completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during conversion: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}
