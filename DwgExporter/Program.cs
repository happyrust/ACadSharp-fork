using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;

namespace DwgExporter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: DwgExporter <input.dwg> [output.json]");
                return;
            }

            string inputPath = args[0];
            string outputPath = args.Length > 1 ? args[1] 
                : Path.ChangeExtension(inputPath, ".acad.json");

            try
            {
                Console.WriteLine($"Loading DWG file: {inputPath}");
                
                using (DwgReader reader = new DwgReader(inputPath))
                {
                    CadDocument doc = reader.Read();
                    
                    var exportData = ExportDocument(doc);
                    
                    var options = new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };
                    
                    string json = JsonSerializer.Serialize(exportData, options);
                    File.WriteAllText(outputPath, json);
                    
                    Console.WriteLine($"Exported {exportData.EntityCount} entities to: {outputPath}");
                    Console.WriteLine($"  Lines: {exportData.Entities.Count(e => e.Type == "Line")}");
                    Console.WriteLine($"  Circles: {exportData.Entities.Count(e => e.Type == "Circle")}");
                    Console.WriteLine($"  Arcs: {exportData.Entities.Count(e => e.Type == "Arc")}");
                    Console.WriteLine($"  Polylines: {exportData.Entities.Count(e => e.Type == "Polyline")}");
                    Console.WriteLine($"  BlockRefs: {exportData.Entities.Count(e => e.Type == "Insert")}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static ExportData ExportDocument(CadDocument doc)
        {
            var entities = new List<EntityExport>();

            foreach (var entity in doc.Entities)
            {
                var exported = ExportEntity(entity);
                if (exported != null)
                {
                    entities.Add(exported);
                }
            }

            return new ExportData
            {
                Version = doc.Header.Version.ToString(),
                EntityCount = entities.Count,
                Entities = entities
            };
        }

        static EntityExport ExportEntity(Entity entity)
        {
            switch (entity)
            {
                case Line line:
                    return new EntityExport
                    {
                        Type = "Line",
                        Handle = line.Handle.ToString("X"),
                        Layer = line.Layer?.Name ?? "0",
                        Start = new[] { line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z },
                        End = new[] { line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z }
                    };

                case Arc arc:
                    return new EntityExport
                    {
                        Type = "Arc",
                        Handle = arc.Handle.ToString("X"),
                        Layer = arc.Layer?.Name ?? "0",
                        Center = new[] { arc.Center.X, arc.Center.Y, arc.Center.Z },
                        Radius = arc.Radius,
                        StartAngle = arc.StartAngle,
                        EndAngle = arc.EndAngle
                    };

                case Circle circle:
                    return new EntityExport
                    {
                        Type = "Circle",
                        Handle = circle.Handle.ToString("X"),
                        Layer =circle.Layer?.Name ?? "0",
                        Center = new[] { circle.Center.X, circle.Center.Y, circle.Center.Z },
                        Radius = circle.Radius
                    };

                case LwPolyline lwpoly:
                    return new EntityExport
                    {
                        Type = "Polyline",
                        Handle = lwpoly.Handle.ToString("X"),
                        Layer = lwpoly.Layer?.Name ?? "0",
                        Vertices = lwpoly.Vertices.Select(v => new[] { v.Location.X, v.Location.Y }).ToList(),
                        IsClosed = (lwpoly.Flags & LwPolylineFlags.Closed) != 0
                    };

                case Insert insert:
                    return new EntityExport
                    {
                        Type = "Insert",
                        Handle = insert.Handle.ToString("X"),
                        Layer = insert.Layer?.Name ?? "0",
                        Insert = new[] { insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z },
                        BlockName = insert.Block?.Name ?? "UNKNOWN",
                        Scale = new[] { insert.XScale, insert.YScale, insert.ZScale },
                        Rotation = insert.Rotation
                    };

                case TextEntity text:
                    return new EntityExport
                    {
                        Type = "Text",
                        Handle = text.Handle.ToString("X"),
                        Layer = text.Layer?.Name ?? "0",
                        Insert = new[] { text.InsertPoint.X, text.InsertPoint.Y, text.InsertPoint.Z },
                        TextValue = text.Value,
                        Height = text.Height,
                        Rotation = text.Rotation
                    };

                default:
                    // Skip unsupported entity types
                    return null;
            }
        }
    }

    class ExportData
    {
        public string Version { get; set; }
        public int EntityCount { get; set; }
        public List<EntityExport> Entities { get; set; }
    }

    class EntityExport
    {
        public string Type { get; set; }
        public string Handle { get; set; }
        public string Layer { get; set; }
        
        // Line
        public double[] Start { get; set; }
        public double[] End { get; set; }
        
        // Circle, Arc
        public double[] Center { get; set; }
        public double Radius { get; set; }
        
        // Arc
        public double? StartAngle { get; set; }
        public double? EndAngle { get; set; }
        
        // Polyline
        public List<double[]> Vertices { get; set; }
        public bool? IsClosed { get; set; }
        
        // Insert
        public double[] Insert { get; set; }
        public string BlockName { get; set; }
        public double[] Scale { get; set; }
        public double? Rotation { get; set; }
        
        // Text
        public string TextValue { get; set; }
        public double? Height { get; set; }
    }
}
