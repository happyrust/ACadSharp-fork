using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using System;
using System.IO;
using System.Linq;

namespace ACadSharp.LegendAnalysis
{
    class Program
    {
        static void Main(string[] args)
        {
            string file = "";
            if (args.Length > 0)
            {
                file = args[0];
            }
            else
            {
                file = Path.Combine(Environment.CurrentDirectory, "../../input-files/2416流程图图例-通风.dwg");
                if (!File.Exists(file))
                {
                     file = "/Volumes/DPC/work/cad-code/ACadSharp/input-files/2416流程图图例-通风.dwg";
                }
            }

            if (!File.Exists(file))
            {
                Console.WriteLine($"Error: File not found: {file}");
                return;
            }

            Console.WriteLine($"Loading file: {file}...");
            CadDocument doc;
            try
            {
                using (DwgReader reader = new DwgReader(file))
                {
                    doc = reader.Read();
                }
                Console.WriteLine("File loaded successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load file: {ex.Message}");
                return;
            }

            Console.WriteLine($"Analyzing {doc.Entities.Count()} entities in ModelSpace...");
            
            var recognizer = new LegendRecognizer();
            recognizer.Run(doc.Entities);

            Console.WriteLine($"Analysis Complete. Found {recognizer.Candidates.Count} candidate clusters.");

            // Count by type
            var grouped = recognizer.Candidates.GroupBy(c => c.DetectedType).OrderByDescending(g => g.Count());
            Console.WriteLine("\n=== Type Distribution ===");
            foreach (var g in grouped)
            {
                Console.WriteLine($"  {g.Key}: {g.Count()}");
            }

            // Get identified legends
            var identified = recognizer.Candidates.Where(c => c.DetectedType != LegendType.Unknown).ToList();
            Console.WriteLine($"\nIdentified {identified.Count} legends.");

            // Analyze Unknown clusters
            var unknowns = recognizer.Candidates.Where(c => c.DetectedType == LegendType.Unknown).ToList();
            Console.WriteLine($"\n=== Unknown Cluster Analysis ({unknowns.Count} total) ===");
            
            // Categorize Unknown clusters
            int hasRect = unknowns.Count(c => c.AnchorRect != null);
            int hasCircles = unknowns.Count(c => c.Entities.OfType<Circle>().Any());
            int hasLines = unknowns.Count(c => c.Entities.OfType<Line>().Count() >= 4);
            int hasText = unknowns.Count(c => c.Entities.OfType<TextEntity>().Any() || c.Entities.OfType<MText>().Any());
            int singleEntity = unknowns.Count(c => c.Entities.Count == 1);
            
            Console.WriteLine($"  Has Anchor Rect: {hasRect}");
            Console.WriteLine($"  Has Circles: {hasCircles}");
            Console.WriteLine($"  Has >=4 Lines: {hasLines}");
            Console.WriteLine($"  Has Text: {hasText}");
            Console.WriteLine($"  Single Entity (noise): {singleEntity}");

            // Sample Unknown clusters by entity count
            var byEntityCount = unknowns.GroupBy(c => c.Entities.Count).OrderByDescending(g => g.Count()).Take(5);
            Console.WriteLine($"\n  Top Unknown cluster sizes:");
            foreach (var g in byEntityCount)
            {
                Console.WriteLine($"    {g.Key} entities: {g.Count()} clusters");
            }

            // Dump a few interesting Unknown clusters
            Console.WriteLine($"\n  Sample Unknown clusters (>1 entity):");
            foreach (var u in unknowns.Where(c => c.Entities.Count > 1).Take(5))
            {
                var types = u.Entities.GroupBy(e => e.GetType().Name).Select(g => $"{g.Key}:{g.Count()}");
                Console.WriteLine($"    @ ({u.BoundingBox.Min.X:F0},{u.BoundingBox.Min.Y:F0}) [{string.Join(", ", types)}]");
            }

            // Create BBOX layer for debug (raw clusters)
            var debugLayer = new Layer("CLUSTER_DEBUG");
            debugLayer.Color = new Color(51); // Brown
            doc.Layers.Add(debugLayer);

            // Draw brown BBOX for ALL raw connectivity clusters (Step 1 result)
            int debugCount = 0;
            foreach (var cluster in recognizer.RawClusters)
            {
                var box = cluster.BoundingBox;
                
                var polyline = new LwPolyline();
                polyline.Layer = debugLayer;
                polyline.Color = new Color(51); // Brown
                
                polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Min.X, box.Min.Y)));
                polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Max.X, box.Min.Y)));
                polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Max.X, box.Max.Y)));
                polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Min.X, box.Max.Y)));
                polyline.IsClosed = true;
                
                doc.Entities.Add(polyline);
                debugCount++;
            }
            Console.WriteLine($"Added {debugCount} brown debug BBOX for raw connectivity clusters.");

            // Create BBOX layer for identified legends
            var bboxLayer = new Layer("LEGEND_BBOX");
            bboxLayer.Color = new Color(1); // Red
            doc.Layers.Add(bboxLayer);

            // Add BBOX rectangles for identified legends
            int addedCount = 0;
            foreach (var candidate in identified)
            {
                var box = candidate.BoundingBox;
                
                // Create a closed polyline as the bounding box
                var polyline = new LwPolyline();
                polyline.Layer = bboxLayer;
                polyline.Color = GetColorForType(candidate.DetectedType);
                
                // Add 4 corners
                polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Min.X, box.Min.Y)));
                polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Max.X, box.Min.Y)));
                polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Max.X, box.Max.Y)));
                polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Min.X, box.Max.Y)));
                polyline.IsClosed = true;
                
                // Add to document
                doc.Entities.Add(polyline);
                addedCount++;
                
                // Optionally add a text label
                var label = new TextEntity();
                label.Layer = bboxLayer;
                label.Value = candidate.DetectedType.ToString();
                label.InsertPoint = new XYZ(box.Min.X, box.Max.Y + 5, 0); // Above the box
                label.Height = 10;
                label.Color = GetColorForType(candidate.DetectedType);
                doc.Entities.Add(label);
            }

            Console.WriteLine($"Added {addedCount} BBOX rectangles and labels to layer 'LEGEND_BBOX'.");

            // Save to new DWG file
            string outputFile = Path.Combine(Path.GetDirectoryName(file)!, 
                Path.GetFileNameWithoutExtension(file) + "_annotated.dwg");
            
            try
            {
                using (DwgWriter writer = new DwgWriter(outputFile, doc))
                {
                    writer.Write();
                }
                Console.WriteLine($"\nSaved annotated DWG to: {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save DWG: {ex.Message}");
                
                // Fallback: try DXF
                string dxfOutput = Path.ChangeExtension(outputFile, ".dxf");
                try
                {
                    using (DxfWriter writer = new DxfWriter(dxfOutput, doc, false))
                    {
                        writer.Write();
                    }
                    Console.WriteLine($"Saved annotated DXF instead: {dxfOutput}");
                }
                catch (Exception dxfEx)
                {
                    Console.WriteLine($"Failed to save DXF: {dxfEx.Message}");
                }
            }

            // === Generate Markdown Report ===
            string mdReportPath = Path.Combine(Path.GetDirectoryName(file)!, 
                Path.GetFileNameWithoutExtension(file) + "_图例识别报告.md");
            
            GenerateMdReport(recognizer.Candidates, doc.Entities.ToList(), mdReportPath);
            Console.WriteLine($"Generated report: {mdReportPath}");

            Console.WriteLine("------------------------------------------------");
            Console.WriteLine($"Summary: Identified {identified.Count} / {recognizer.Candidates.Count} legends.");
        }

        static void GenerateMdReport(System.Collections.Generic.List<LegendCandidate> candidates, System.Collections.Generic.List<Entity> allEntities, string path)
        {
            using var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8);
            sw.WriteLine("# 图例识别报告");
            sw.WriteLine($"\n生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            sw.WriteLine("---\n");

            var identified = candidates.Where(c => c.DetectedType != LegendType.Unknown).ToList();
            var unknowns = candidates.Where(c => c.DetectedType == LegendType.Unknown).ToList();
            
            // Collect all text entities (for label lookup)
            var allTexts = allEntities.OfType<TextEntity>().ToList();
            var allMTexts = allEntities.OfType<MText>().ToList();
            
            // Summary
            sw.WriteLine("## 识别总览\n");
            sw.WriteLine($"| 指标 | 数值 |");
            sw.WriteLine($"| :--- | :--- |");
            sw.WriteLine($"| 总集群数 | {candidates.Count} |");
            sw.WriteLine($"| 已识别图例 | {identified.Count} |");
            sw.WriteLine($"| 未识别集群 | {unknowns.Count} |");
            sw.WriteLine();

            // Type distribution
            sw.WriteLine("### 类型分布\n");
            sw.WriteLine("| 图例类型 | 数量 | 状态 |");
            sw.WriteLine("| :--- | :--- | :--- |");
            foreach (var g in candidates.GroupBy(c => c.DetectedType).OrderByDescending(g => g.Key != LegendType.Unknown).ThenByDescending(g => g.Count()))
            {
                string status = g.Key == LegendType.Unknown ? "❓" : "✅";
                sw.WriteLine($"| {g.Key} | {g.Count()} | {status} |");
            }
            sw.WriteLine();

            // Identified Legends with right-side label
            sw.WriteLine("---\n");
            sw.WriteLine("## 已识别图例清单\n");
            sw.WriteLine("| # | 识别类型 | 图纸标签 | 位置 (X, Y) | 尺寸 (W x H) | 实体数 |");
            sw.WriteLine("| :--- | :--- | :--- | :--- | :--- | :--- |");
            
            int idx = 1;
            foreach (var c in identified.OrderBy(c => c.DetectedType.ToString()))
            {
                var box = c.BoundingBox;
                double w = box.Max.X - box.Min.X;
                double h = box.Max.Y - box.Min.Y;
                
                // Find nearest right-side label
                string label = FindRightSideLabel(box, allTexts, allMTexts);
                
                sw.WriteLine($"| {idx++} | {c.DetectedType} | {label} | ({box.Min.X:F0}, {box.Min.Y:F0}) | {w:F0} x {h:F0} | {c.Entities.Count} |");
            }
            sw.WriteLine();

            // Text labels reference
            sw.WriteLine("---\n");
            sw.WriteLine("## 图纸中的绿色文字标签\n");
            sw.WriteLine("| 文字内容 | 位置 |");
            sw.WriteLine("| :--- | :--- |");
            
            var greenTexts = allEntities.OfType<TextEntity>().Where(t => t.Color.Index == 3).Take(30);
            foreach (var t in greenTexts)
            {
                sw.WriteLine($"| {t.Value.Replace("|", "\\|").Trim()} | ({t.InsertPoint.X:F0}, {t.InsertPoint.Y:F0}) |");
            }
            sw.WriteLine();
            
            // Unknown clusters
            sw.WriteLine("---\n");
            sw.WriteLine("## 未识别集群\n");
            sw.WriteLine($"- 单实体噪声: {unknowns.Count(c => c.Entities.Count == 1)}");
            sw.WriteLine($"- 多实体集群: {unknowns.Count(c => c.Entities.Count > 1)}");
        }

        /// <summary>
        /// Find the nearest text label to the RIGHT of the given bounding box.
        /// The text must be within a reasonable Y-range and to the right of box.Max.X.
        /// </summary>
        static string FindRightSideLabel(BoundingBox box, System.Collections.Generic.List<TextEntity> texts, System.Collections.Generic.List<MText> mtexts)
        {
            double boxCenterY = (box.Min.Y + box.Max.Y) / 2;
            double boxHeight = box.Max.Y - box.Min.Y;
            double yTolerance = Math.Max(boxHeight * 2, 30); // Allow some vertical flexibility
            double maxSearchX = 300; // Don't search too far
            
            string? bestLabel = null;
            double bestDist = double.MaxValue;
            
            foreach (var t in texts)
            {
                var pt = t.InsertPoint;
                // Must be to the right of the box
                if (pt.X <= box.Max.X) continue;
                // Must be within Y tolerance
                if (Math.Abs(pt.Y - boxCenterY) > yTolerance) continue;
                // Must be within X search range
                double dx = pt.X - box.Max.X;
                if (dx > maxSearchX) continue;
                
                if (dx < bestDist)
                {
                    bestDist = dx;
                    bestLabel = t.Value.Trim();
                }
            }
            
            foreach (var t in mtexts)
            {
                var pt = t.InsertPoint;
                if (pt.X <= box.Max.X) continue;
                if (Math.Abs(pt.Y - boxCenterY) > yTolerance) continue;
                double dx = pt.X - box.Max.X;
                if (dx > maxSearchX) continue;
                
                if (dx < bestDist)
                {
                    bestDist = dx;
                    bestLabel = t.Value.Trim();
                }
            }
            
            return bestLabel ?? "-";
        }

        static Color GetColorForType(LegendType type)
        {
            return type switch
            {
                LegendType.CheckValve => new Color(3),        // Green
                LegendType.LimitSwitch => new Color(5),       // Blue  
                LegendType.FreshAirLouver => new Color(4),    // Cyan
                LegendType.ExhaustLouver => new Color(6),     // Magenta
                LegendType.FireDamper => new Color(1),        // Red
                LegendType.SmokeFireDamper => new Color(30),  // Orange
                LegendType.ElectricIsolationValve => new Color(2), // Yellow
                LegendType.ManualIsolationValve => new Color(51),  // Brown
                _ => new Color(7) // White
            };
        }
    }
}

