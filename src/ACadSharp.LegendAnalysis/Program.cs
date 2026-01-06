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
                // 优先默认使用 dxf，再回退 dwg
                var dxfDefault = Path.Combine(Environment.CurrentDirectory, "../../input-files/2416流程图图例-通风.dxf");
                var dwgDefault = Path.Combine(Environment.CurrentDirectory, "../../input-files/2416流程图图例-通风.dwg");
                file = File.Exists(dxfDefault) ? dxfDefault : dwgDefault;
                if (!File.Exists(file))
                {
                    var baseDir = "/Volumes/DPC/work/cad-code/ACadSharp/input-files/";
                    if (File.Exists(Path.Combine(baseDir, "2416流程图图例-通风.dxf")))
                    {
                        file = Path.Combine(baseDir, "2416流程图图例-通风.dxf");
                    }
                    else
                    {
                        file = Path.Combine(baseDir, "2416流程图图例-通风.dwg");
                    }
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
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".dxf")
                {
                    using (DxfReader reader = new DxfReader(file))
                    {
                        doc = reader.Read();
                    }
                }
                else
                {
                    using (DwgReader reader = new DwgReader(file))
                    {
                        doc = reader.Read();
                    }
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
                double w = u.BoundingBox.Max.X - u.BoundingBox.Min.X;
                double h = u.BoundingBox.Max.Y - u.BoundingBox.Min.Y;
                Console.WriteLine($"    @ ({u.BoundingBox.Min.X:F0},{u.BoundingBox.Min.Y:F0}) Size: {w:F0}x{h:F0} [{string.Join(", ", types)}]");
                
                // Debug Inserts
                var inserts = u.Entities.OfType<Insert>().ToList();
                if (inserts.Any())
                {
                    Console.WriteLine($"      Block Names: {string.Join(", ", inserts.Select(i => i.Block.Name))}");
                }
            }
            
            Console.WriteLine("\n   Identified Clusters Sizes:");
            foreach (var c in identified)
            {
                double w = c.BoundingBox.Max.X - c.BoundingBox.Min.X;
                double h = c.BoundingBox.Max.Y - c.BoundingBox.Min.Y;
                Console.WriteLine($"    [{c.DetectedType}] Size: {w:F0}x{h:F0} @ ({c.BoundingBox.Min.X:F0},{c.BoundingBox.Min.Y:F0})");
            }

            // --- Legend Grouping & Annotation ---
            var legendLayer = new Layer("LEGEND_BBOX");
            legendLayer.Color = new Color(1); // Red
            doc.Layers.Add(legendLayer);

            var annotationLayer = new Layer("FEATURE_ANNOTATIONS");
            annotationLayer.Color = new Color(1); // Red
            doc.Layers.Add(annotationLayer);

            int totalFeatures = 0;
            foreach (var group in recognizer.Groups)
            {
                // 1. Draw Group BBOX (Consolidated)
                DrawBox(doc, legendLayer, group.BoundingBox, new Color(1));
                
                // 2. Add Group Label at top-left
                var groupLabel = new MText();
                groupLabel.Layer = legendLayer;
                groupLabel.Color = new Color(1);
                groupLabel.Value = $"GROUP-{group.Index}";
                groupLabel.InsertPoint = new XYZ(group.BoundingBox.Min.X, group.BoundingBox.Max.Y + 2.0, 0);
                groupLabel.Height = 5.0;
                doc.Entities.Add(groupLabel);

                // 3. Draw individual features within the group
                foreach (var member in group.Members)
                {
                    if (member.DetectedType == LegendType.Unknown) continue;

                    // Draw individual wavy line (cloud) for the feature with its ID
                    string label = $"{member.DetectedType} (ID:{member.Index})";
                    DrawCloudBox(doc, annotationLayer, member.BoundingBox, new Color(1), label);
                    
                    totalFeatures++;
                }
            }

            Console.WriteLine($"\nAdded {recognizer.Groups.Count} group boxes and {totalFeatures} wavy annotations.");
            // --- Final Output Save ---
            string outputFile = Path.Combine(Path.GetDirectoryName(file)!,
                Path.GetFileNameWithoutExtension(file) + "_annotated.dwg");

            try
            {
                using var writer = new DwgWriter(outputFile, doc);
                writer.Write();
                Console.WriteLine($"\nSaved annotated DWG to: {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save annotated DWG: {ex.Message}");
                // Fallback: try DXF
                string dxfOutput = Path.ChangeExtension(outputFile, ".dxf");
                try
                {
                    using var writer = new DxfWriter(dxfOutput, doc, false);
                    writer.Write();
                    Console.WriteLine($"Saved annotated DXF instead: {dxfOutput}");
                }
                catch (Exception dxfEx) { Console.WriteLine($"Failed to save DXF: {dxfEx.Message}"); }
            }

            // --- Generate Markdown Report ---
            string mdReportPath = Path.Combine(Path.GetDirectoryName(file)!, 
                Path.GetFileNameWithoutExtension(file) + "_图例识别报告.md");
            
            GenerateMdReport(recognizer.Candidates, doc.Entities.ToList(), mdReportPath);
            Console.WriteLine($"Generated report: {mdReportPath}");

            Console.WriteLine("------------------------------------------------");
            Console.WriteLine($"Summary: Identified {identified.Count} / {recognizer.Candidates.Count} legends across {recognizer.Groups.Count} groups.");
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

	        static void SaveBboxDebug(List<Entity> allEntities, string path)
	        {
	            // 1) 噪声过滤
	            var filtered = allEntities.Where(e =>
	            {
	                switch (e)
	                {
	                    case Line l:
	                        return Math.Sqrt(Math.Pow(l.StartPoint.X - l.EndPoint.X, 2) + Math.Pow(l.StartPoint.Y - l.EndPoint.Y, 2)) > 3.0;
	                    case Circle c:
	                        return c.Radius * 2.0 > 3.0;
	                    case LwPolyline p:
	                        return p.Vertices.Count >= 2;
	                    default:
	                        return true;
	                }
	            }).ToList();

	            // 2) 原始 BBOX 列表
	            var rawBoxes = new List<BoundingBox>();
	            foreach (var e in filtered)
	            {
	                var b = e.GetBoundingBox();
	                if (b.Max.X < b.Min.X || b.Max.Y < b.Min.Y) continue;
	                double w = b.Max.X - b.Min.X;
	                double h = b.Max.Y - b.Min.Y;
	                if (w < 1.0 && h < 1.0) continue;
	                rawBoxes.Add(b);
	            }

	            // 3) 合并
	            var merged = MergeByBoundingBoxes(rawBoxes, 1.0);

	            // 4) 输出 DWG
	            var bboxDoc = new CadDocument();
	            var rawLayer = new Layer("RAW_BBOX") { Color = new Color(51) };
	            var mergedLayer = new Layer("MERGED_BBOX") { Color = new Color(140) };
	            bboxDoc.Layers.Add(rawLayer);
	            bboxDoc.Layers.Add(mergedLayer);

	            foreach (var box in rawBoxes)
	            {
	                DrawBox(bboxDoc, rawLayer, box, new Color(51));
	            }
	            foreach (var box in merged)
	            {
	                DrawBox(bboxDoc, mergedLayer, box, new Color(140));
	            }

	            try
	            {
	                using var writer = new DwgWriter(path, bboxDoc);
	                writer.Write();
	                Console.WriteLine($"Saved bbox debug DWG to: {path} (raw {rawBoxes.Count}, merged {merged.Count})");
	            }
	            catch (Exception ex)
	            {
	                Console.WriteLine($"Failed to save bbox DWG: {ex.Message}");
	            }
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

		static void SaveBboxDebug(List<BoundingBox> rawBoxes, List<BoundingBox> mergedBoxes, string path)
		{
			var bboxDoc = new CadDocument();
			var rawLayer = new Layer("RAW_BBOX") { Color = new Color(51) };
			var mergedLayer = new Layer("MERGED_BBOX") { Color = new Color(140) };
			bboxDoc.Layers.Add(rawLayer);
			bboxDoc.Layers.Add(mergedLayer);

			foreach (var box in rawBoxes)
			{
				DrawBox(bboxDoc, rawLayer, box, new Color(51));
			}
			foreach (var box in mergedBoxes)
			{
				DrawBox(bboxDoc, mergedLayer, box, new Color(140));
			}

			try
			{
				using var writer = new DwgWriter(path, bboxDoc);
				writer.Write();
				Console.WriteLine($"Saved bbox debug DWG to: {path}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed to save bbox DWG: {ex.Message}");
			}
		}

		static void DrawCloudBox(CadDocument doc, Layer layer, BoundingBox box, Color color, string id)
		{
			var cloud = new LwPolyline();
			cloud.Layer = layer;
			cloud.Color = color;
			
			double w = box.Max.X - box.Min.X;
			double h = box.Max.Y - box.Min.Y;
			
			double margin = Math.Max(Math.Min(w, h) * 0.2, 2.0);
			double minX = box.Min.X - margin;
			double minY = box.Min.Y - margin;
			double maxX = box.Max.X + margin;
			double maxY = box.Max.Y + margin;
			
			double arcSize = Math.Max(Math.Min(w, h) * 0.3, 5.0);
			if (arcSize > 30) arcSize = 30;

			void AddWavyEdge(XYZ start, XYZ end)
			{
				double dist = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
				int segments = (int)Math.Max(Math.Ceiling(dist / arcSize), 1);
				double dx = (end.X - start.X) / segments;
				double dy = (end.Y - start.Y) / segments;
				
				for (int i = 0; i < segments; i++)
				{
					var v = new LwPolyline.Vertex(new XY(start.X + i * dx, start.Y + i * dy));
					v.Bulge = 0.5;
					cloud.Vertices.Add(v);
				}
			}

			AddWavyEdge(new XYZ(minX, minY, 0), new XYZ(minX, maxY, 0));
			AddWavyEdge(new XYZ(minX, maxY, 0), new XYZ(maxX, maxY, 0));
			AddWavyEdge(new XYZ(maxX, maxY, 0), new XYZ(maxX, minY, 0));
			AddWavyEdge(new XYZ(maxX, minY, 0), new XYZ(minX, minY, 0));

			cloud.IsClosed = true;
			doc.Entities.Add(cloud);

			// Add ID Label
			var label = new MText();
			label.Layer = layer;
			label.Color = color;
			label.Value = id;
			label.InsertPoint = new XYZ(minX, maxY + 2.0, 0);
			label.Height = Math.Max(arcSize * 0.8, 8.0);
			doc.Entities.Add(label);
		}

		static void DrawBox(CadDocument doc, Layer layer, BoundingBox box, Color color)
		{
			var polyline = new LwPolyline();
			polyline.Layer = layer;
			polyline.Color = color;
			
			polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Min.X, box.Min.Y)));
			polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Max.X, box.Min.Y)));
			polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Max.X, box.Max.Y)));
			polyline.Vertices.Add(new LwPolyline.Vertex(new XY(box.Min.X, box.Max.Y)));
			polyline.IsClosed = true;
			
			doc.Entities.Add(polyline);
		}

		static List<BoundingBox> MergeByBoundingBoxes(List<BoundingBox> boxes, double tolerance)
		{
			int n = boxes.Count;
			if (n <= 1) return boxes;

			int[] parent = new int[n];
			for (int i = 0; i < n; i++) parent[i] = i;

			int Find(int x)
			{
				if (parent[x] != x) parent[x] = Find(parent[x]);
				return parent[x];
			}
			void Unite(int x, int y)
			{
				int px = Find(x), py = Find(y);
				if (px != py) parent[px] = py;
			}

			for (int i = 0; i < n; i++)
			{
				for (int j = i + 1; j < n; j++)
				{
					var a = boxes[i];
					var b = boxes[j];
					if (Intersects(a, b) || BoxesAreNear(a, b, tolerance) ||
						IsBoxInside(a, b, tolerance) || IsBoxInside(b, a, tolerance))
					{
						Unite(i, j);
					}
				}
			}

			var merged = new Dictionary<int, List<BoundingBox>>();
			for (int i = 0; i < n; i++)
			{
				int root = Find(i);
				if (!merged.ContainsKey(root)) merged[root] = new List<BoundingBox>();
				merged[root].Add(boxes[i]);
			}

			var result = new List<BoundingBox>();
			foreach (var g in merged.Values)
			{
				double minx = g.Min(b => b.Min.X);
				double miny = g.Min(b => b.Min.Y);
				double maxx = g.Max(b => b.Max.X);
				double maxy = g.Max(b => b.Max.Y);
				result.Add(new BoundingBox(new XYZ(minx, miny, 0), new XYZ(maxx, maxy, 0)));
			}
			return result;
		}

		static bool BoxesAreNear(BoundingBox a, BoundingBox b, double tolerance)
		{
			double gapX = Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X);
			double gapY = Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y);
			return gapX <= tolerance && gapY <= tolerance;
		}

		static bool IsBoxInside(BoundingBox outer, BoundingBox inner, double tolerance)
		{
			return inner.Min.X >= outer.Min.X - tolerance &&
				   inner.Min.Y >= outer.Min.Y - tolerance &&
				   inner.Max.X <= outer.Max.X + tolerance &&
				   inner.Max.Y <= outer.Max.Y + tolerance;
		}

		static bool Intersects(BoundingBox a, BoundingBox b)
		{
			return (a.Min.X <= b.Max.X && a.Max.X >= b.Min.X) &&
				   (a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y);
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
                LegendType.CoolingUnit => new Color(141),     // Teal-ish
                LegendType.AirHandlingUnit => new Color(143), // Aqua
                LegendType.SplitCabinet => new Color(171),    // Light green
                LegendType.Silencer => new Color(140),        // Light blue-green
                _ => new Color(7) // White
            };
        }
    }
}
