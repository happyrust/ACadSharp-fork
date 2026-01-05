using ACadSharp.Entities;
using CSMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACadSharp.LegendAnalysis
{
    public class LegendRecognizer
    {
        public List<LegendCandidate> Candidates { get; private set; } = new List<LegendCandidate>();
        
        /// <summary>
        /// Raw clusters from connectivity phase (before identification/merge) for debugging.
        /// </summary>
        public List<LegendCandidate> RawClusters { get; private set; } = new List<LegendCandidate>();

        public void Run(IEnumerable<Entity> entities)
        {
            var allEntities = entities.ToList();

            // 1. Filter: Only Green entities (effective color)
            var greenEntities = allEntities.Where(IsGreen).ToList();
            
            // 2. Cluster
            // Tolerance 0.1 (Strict) with Point-on-Circle logic
            Candidates = ClusterEntities(greenEntities, 0.1);
            
            // Save raw clusters for debugging visualization
            RawClusters = Candidates.Select(c => new LegendCandidate
            {
                Entities = c.Entities,
                BoundingBox = c.BoundingBox
            }).ToList();

            // 3. Merge clusters by anchor containment/top proximity
            Candidates = MergeClustersByAnchorProximity(Candidates);

            // 4. Attach nearby text context (all colors)
            AttachContextEntities(Candidates, allEntities);

            // 5. Identify each cluster
            foreach (var candidate in Candidates)
            {
                candidate.DetectedType = IdentifyLegend(candidate);
            }

            // 6. Post-process: Merge adjacent candidates of the same type
            // This handles cases where a legend is fragmented into multiple clusters
            // Tolerance 3.0: merge only tightly connected fragments
            Candidates = MergeSameTypeCandidates(Candidates, 3.0);
        }

        /// <summary>
        /// Merge candidates of the same type whose bounding boxes are within tolerance distance.
        /// </summary>
        private List<LegendCandidate> MergeSameTypeCandidates(List<LegendCandidate> candidates, double tolerance)
        {
            // Group by type first
            var groups = candidates.GroupBy(c => c.DetectedType).ToList();
            var result = new List<LegendCandidate>();
            
            foreach (var group in groups)
            {
                if (group.Key == LegendType.Unknown)
                {
                    // Don't merge Unknown clusters
                    result.AddRange(group);
                    continue;
                }

                var sameCandidates = group.ToList();
                int n = sameCandidates.Count;
                if (n <= 1)
                {
                    result.AddRange(sameCandidates);
                    continue;
                }

                // Union-Find to merge nearby candidates
                int[] parent = new int[n];
                for (int i = 0; i < n; i++) parent[i] = i;

                int Find(int x) {
                    if (parent[x] != x) parent[x] = Find(parent[x]);
                    return parent[x];
                }
                void Unite(int x, int y) {
                    int px = Find(x), py = Find(y);
                    if (px != py) parent[px] = py;
                }

                // Check bounding box proximity
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        if (BoxesAreNear(sameCandidates[i].BoundingBox, sameCandidates[j].BoundingBox, tolerance))
                        {
                            Unite(i, j);
                        }
                    }
                }

                // Group and merge
                var merged = new Dictionary<int, List<LegendCandidate>>();
                for (int i = 0; i < n; i++)
                {
                    int root = Find(i);
                    if (!merged.ContainsKey(root)) merged[root] = new List<LegendCandidate>();
                    merged[root].Add(sameCandidates[i]);
                }

                foreach (var m in merged.Values)
                {
                    if (m.Count == 1)
                    {
                        result.Add(m[0]);
                    }
                    else
                    {
                        // Merge into a single candidate
                        var allEntities = m.SelectMany(c => c.Entities).ToList();
                        var contextEntities = m.SelectMany(c => c.ContextEntities.Any() ? c.ContextEntities : c.Entities).Distinct().ToList();
                        var mergedBox = GetBoundingBox(allEntities);
                        result.Add(new LegendCandidate
                        {
                            Entities = allEntities,
                            ContextEntities = contextEntities,
                            BoundingBox = mergedBox,
                            DetectedType = m[0].DetectedType,
                            AnchorRect = m[0].AnchorRect // Use first anchor
                        });
                    }
                }
            }
            
            return result;
        }

        private List<LegendCandidate> MergeClustersByAnchorProximity(List<LegendCandidate> candidates)
        {
            int n = candidates.Count;
            if (n <= 1) return candidates;

            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x) {
                if (parent[x] != x) parent[x] = Find(parent[x]);
                return parent[x];
            }
            void Unite(int x, int y) {
                int px = Find(x), py = Find(y);
                if (px != py) parent[px] = py;
            }

            var rectBoxes = new BoundingBox?[n];
            for (int i = 0; i < n; i++)
            {
                var rect = FindMainRectangle(candidates[i]);
                if (rect != null)
                {
                    rectBoxes[i] = GetBoundingBox(rect);
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (rectBoxes[i] == null) continue;
                var rectBox = rectBoxes[i]!.Value;
                double rectW = rectBox.Max.X - rectBox.Min.X;
                double rectH = rectBox.Max.Y - rectBox.Min.Y;
                double containmentTol = Math.Max(Math.Min(rectW, rectH) * 0.05, 1.0);
                double topGap = Math.Min(Math.Max(rectH * 0.6, 5.0), 30.0);
                double sideTol = Math.Min(Math.Max(rectW * 0.2, 5.0), 30.0);

                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    if (Find(i) == Find(j)) continue;
                    var otherBox = candidates[j].BoundingBox;

                    if (IsBoxInside(rectBox, otherBox, containmentTol) ||
                        IsTopAdjacent(rectBox, otherBox, topGap, sideTol))
                    {
                        Unite(i, j);
                    }
                }
            }

            var merged = new Dictionary<int, List<LegendCandidate>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!merged.ContainsKey(root)) merged[root] = new List<LegendCandidate>();
                merged[root].Add(candidates[i]);
            }

            var result = new List<LegendCandidate>();
            foreach (var m in merged.Values)
            {
                if (m.Count == 1)
                {
                    result.Add(m[0]);
                    continue;
                }

                var allEntities = m.SelectMany(c => c.Entities).ToList();
                var mergedBox = GetBoundingBox(allEntities);
                result.Add(new LegendCandidate
                {
                    Entities = allEntities,
                    BoundingBox = mergedBox
                });
            }

            return result;
        }

        private void AttachContextEntities(List<LegendCandidate> candidates, List<Entity> allEntities)
        {
            var textEntities = allEntities.Where(e => e is TextEntity || e is MText).ToList();
            if (!textEntities.Any())
            {
                foreach (var c in candidates)
                {
                    c.ContextEntities = c.Entities.ToList();
                }
                return;
            }

            foreach (var candidate in candidates)
            {
                var box = candidate.BoundingBox;
                double w = box.Max.X - box.Min.X;
                double h = box.Max.Y - box.Min.Y;
                double minDim = Math.Min(w, h);
                double margin = Math.Min(Math.Max(minDim * 0.6, 10.0), 40.0);
                var expanded = ExpandBox(box, margin);

                var attached = new List<Entity>();
                foreach (var t in textEntities)
                {
                    if (TryGetTextInsertPoint(t, out var pt) && IsInside(pt, expanded))
                    {
                        attached.Add(t);
                    }
                }

                candidate.ContextEntities = candidate.Entities.Concat(attached).Distinct().ToList();
            }
        }

        /// <summary>
        /// Check if two bounding boxes are within tolerance distance (gap between edges).
        /// </summary>
        private bool BoxesAreNear(BoundingBox a, BoundingBox b, double tolerance)
        {
            // Calculate gap between boxes (negative means overlap)
            double gapX = Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X);
            double gapY = Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y);
            
            // Both gaps must be within tolerance
            return gapX <= tolerance && gapY <= tolerance;
        }

        private bool IsGreen(Entity e)
        {
            var color = e.GetActiveColor();
            if (color.IsTrueColor)
            {
                return color.GetApproxIndex() == 3;
            }
            return color.Index == 3;
        }

        private List<LegendCandidate> ClusterEntities(IEnumerable<Entity> entities, double tolerance = 0.1)
        {
            var entityList = entities.ToList();
            int n = entityList.Count;
            if (n == 0) return new List<LegendCandidate>();

            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x) {
                if (parent[x] != x) parent[x] = Find(parent[x]);
                return parent[x];
            }
            void Unite(int x, int y) {
                int px = Find(x), py = Find(y);
                if (px != py) parent[px] = py;
            }

            // Cache checkpoints (endpoints) for all entities
            var cache = new List<List<XYZ>>(n);
            for (int i = 0; i < n; i++) cache.Add(GetCheckPoints(entityList[i]));

            for (int i = 0; i < n; i++)
            {
                var entA = entityList[i];
                var ptsA = cache[i];

                for (int j = i + 1; j < n; j++)
                {
                    if (Find(i) == Find(j)) continue;

                    var entB = entityList[j];
                    var ptsB = cache[j];

                    // Check bidirectional: Endpoints of A on Geometry of B OR Endpoints of B on Geometry of A
                    if (IsConnected(entA, ptsA, entB, ptsB, tolerance))
                    {
                        Unite(i, j);
                    }
                }
            }

            var groups = new Dictionary<int, List<Entity>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!groups.ContainsKey(root)) groups[root] = new List<Entity>();
                groups[root].Add(entityList[i]);
            }

            return groups.Values.Select(c => new LegendCandidate 
            { 
                Entities = c,
                BoundingBox = GetBoundingBox(c)
            }).ToList();
        }

        private List<XYZ> GetCheckPoints(Entity e)
        {
            var pts = new List<XYZ>();
            switch (e)
            {
                case Line line:
                    pts.Add(line.StartPoint);
                    pts.Add(line.EndPoint);
                    break;
                case LwPolyline pl:
                    foreach (var v in pl.Vertices)
                        pts.Add(new XYZ(v.Location.X, v.Location.Y, 0));
                    break;
                case Arc arc:
                    arc.GetEndVertices(out var arcStart, out var arcEnd);
                    pts.Add(arcStart);
                    pts.Add(arcEnd);
                    break;
                case Circle circle:
                    pts.Add(circle.Center);
                    break;
                case TextEntity text:
                    pts.Add(text.InsertPoint);
                    break;
                case MText mtext:
                    pts.Add(mtext.InsertPoint);
                    break;
            }
            return pts;
        }

        private bool IsConnected(Entity entA, List<XYZ> ptsA, Entity entB, List<XYZ> ptsB, double tol)
        {
            // 1. Quick Vertex-to-Vertex Check
            double tolSq = tol * tol;
            foreach (var pa in ptsA)
            {
                foreach (var pb in ptsB)
                {
                    double dx = pa.X - pb.X;
                    double dy = pa.Y - pb.Y;
                    if (dx * dx + dy * dy <= tolSq) return true;
                }
            }

            // 2. Vertex-to-Edge Check (A's points on B, or B's points on A)
            if (CheckPointsOnEntity(entB, ptsA, tol)) return true;
            if (CheckPointsOnEntity(entA, ptsB, tol)) return true;

            return false;
        }

        private bool CheckPointsOnEntity(Entity target, List<XYZ> points, double tol)
        {
            switch (target)
            {
                case Line l:
                    return points.Any(p => DistToSegment(p, l.StartPoint, l.EndPoint) <= tol);
                case Circle c:
                    return points.Any(p => Math.Abs(Distance(p, c.Center) - c.Radius) <= tol);
                case LwPolyline pl:
                    if (pl.Vertices.Count < 2) return false;
                    // Check dist to each segment
                    int cnt = pl.Vertices.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        var p1 = pl.Vertices[i].Location;
                        var p2 = pl.Vertices[(i + 1) % cnt].Location; // Closed or not? 
                        if (!pl.IsClosed && i == cnt - 1) break;
                        
                        var v1 = new XYZ(p1.X, p1.Y, 0);
                        var v2 = new XYZ(p2.X, p2.Y, 0);
                        
                        if (points.Any(p => DistToSegment(p, v1, v2) <= tol)) return true;
                    }
                    return false;
            }
            return false;
        }

        private double DistToSegment(XYZ p, XYZ v, XYZ w)
        {
            double l2 = Math.Pow(v.X - w.X, 2) + Math.Pow(v.Y - w.Y, 2);
            if (l2 == 0) return Distance(p, v);
            double t = ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2;
            t = Math.Max(0, Math.Min(1, t));
            var proj = new XYZ(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y), 0);
            return Distance(p, proj);
        }

        private List<Entity> GetContextEntities(LegendCandidate candidate)
        {
            if (candidate.ContextEntities != null && candidate.ContextEntities.Any())
            {
                return candidate.ContextEntities;
            }

            return candidate.Entities;
        }

        private LegendType IdentifyLegend(LegendCandidate candidate)
        {
            var contextEntities = GetContextEntities(candidate);
            var rect = FindMainRectangle(candidate);
            candidate.AnchorRect = rect;

            if (rect == null)
            {
                if (IsLimitSwitch(candidate)) return LegendType.LimitSwitch;
                return LegendType.Unknown;
            }

            var bbox = GetBoundingBox(rect);
            double w = bbox.Max.X - bbox.Min.X;
            double h = bbox.Max.Y - bbox.Min.Y;
            if (h == 0) h = 0.001;
            double ratio = Math.Max(w, h) / Math.Min(w, h);

            string text = GetTextInZone(contextEntities, bbox, Zone.Center);
            bool hasHatch = candidate.Entities.Any(e => e is Hatch);
            bool hasDiagonal = HasDiagonalInRect(candidate.Entities, bbox);
            bool hasWShape = HasWShapeInRect(candidate.Entities, bbox);

            if (ratio > 2.5) // Narrow
            {
                bool dense = hasHatch || HasManyLines(candidate.Entities);
                if (dense)
                {
                    var dir = CheckArrowDirection(candidate.Entities, bbox);
                    if (dir == Direction.Inward) return LegendType.FreshAirLouver;
                    if (dir == Direction.Outward) return LegendType.ExhaustLouver;
                    return LegendType.FreshAirLouver; // Fallback
                }
            }
            else // Square
            {
                if (text.Contains("F")) return LegendType.FireDamper;
                if (text.Contains("E")) return LegendType.SmokeFireDamper;

                var topFeature = AnalyzeZoneTop(contextEntities, bbox);
                switch (topFeature)
                {
                    case TopFeature.CircleM: return LegendType.ElectricIsolationValve;
                    case TopFeature.TShape: return LegendType.ManualIsolationValve;
                    case TopFeature.LShape: return LegendType.GravityReliefValve;
                    case TopFeature.Zigzag: return LegendType.BlastValve;
                    case TopFeature.None: 
                    default:
                         if (hasWShape) return LegendType.BlastValve;
                         if (hasDiagonal || HasManyLines(candidate.Entities)) return LegendType.CheckValve;
                         return LegendType.Unknown;
                }
            }

            return LegendType.Unknown;
        }

        private Entity? FindMainRectangle(LegendCandidate candidate)
        {
            // Priority 1: LwPolyline approximating rectangle
            var polylines = candidate.Entities.OfType<LwPolyline>().Where(p => p.IsClosed).ToList();
            var rectPolylines = polylines.Where(IsRectanglePolyline).ToList();
            if (rectPolylines.Any())
            {
                return rectPolylines.OrderByDescending(p => Area(p)).First();
            }
            
            // Priority 2: 4 connected Lines
            var lines = candidate.Entities.OfType<Line>().ToList();
            if (lines.Count >= 4)
            {
                var rect = TryFind4LineRectangle(lines);
                if (rect != null) return rect;
            }
            
            return null;
        }

        private LwPolyline? TryFind4LineRectangle(List<Line> lines, double tolerance = 5.0)
        {
            // Strategy: Find any subset of 4 lines that form a closed loop
            int n = lines.Count;
            if (n < 4) return null;
            if (n > 50) return null; // Increased limit to 50 (approx 230k checks max)

            // Brute force combinations of 4 lines
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        for (int m = k + 1; m < n; m++)
                        {
                            var subset = new List<Line> { lines[i], lines[j], lines[k], lines[m] };
                            if (IsClosedLoop(subset, tolerance, out var corners))
                            {
                                // Found a rectangle!
                                var poly = new LwPolyline();
                                foreach (var c in corners)
                                {
                                    poly.Vertices.Add(new LwPolyline.Vertex(new CSMath.XY(c.X, c.Y)));
                                }
                                poly.IsClosed = true;
                                return poly;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private bool IsClosedLoop(List<Line> lines, double tolerance, out List<XYZ> orderedCorners)
        {
            orderedCorners = new List<XYZ>();
            
            // We have 4 lines. 
            // We need to trace them L1 -> L2 -> L3 -> L4 -> L1
            
            var remaining = new List<Line>(lines);
            var currentLine = remaining[0];
            remaining.RemoveAt(0);
            
            var startPt = currentLine.StartPoint;
            var currentPt = currentLine.EndPoint;
            orderedCorners.Add(startPt);
            orderedCorners.Add(currentPt);

            // Try to find chain of 3 more lines
            for (int step = 0; step < 3; step++)
            {
                Line? next = null;
                bool reverse = false;
                
                foreach (var r in remaining)
                {
                    if (Distance(r.StartPoint, currentPt) < tolerance)
                    {
                        next = r;
                        reverse = false;
                        break;
                    }
                    else if (Distance(r.EndPoint, currentPt) < tolerance)
                    {
                        next = r;
                        reverse = true;
                        break;
                    }
                }
                
                if (next == null) return false; // Broken chain
                
                remaining.Remove(next);
                currentLine = next;
                currentPt = reverse ? next.StartPoint : next.EndPoint;
                
                if (step < 2) orderedCorners.Add(currentPt); // Don't add the last point (it should be startPt)
            }
            
            // Check closure
            return Distance(currentPt, startPt) < tolerance;
        }

        private bool IsLimitSwitch(LegendCandidate candidate)
        {
            var circles = candidate.Entities.OfType<Circle>().Where(c => c.Radius < 100).ToList();
            return circles.Count >= 3;
        }

        private string GetTextInZone(IEnumerable<Entity> entities, BoundingBox refBox, Zone zone)
        {
            var texts = entities.OfType<TextEntity>().Where(t => IsInside(t.InsertPoint, refBox));
            var mtexts = entities.OfType<MText>().Where(t => IsInside(t.InsertPoint, refBox));
            
            string combined = string.Join("", texts.Select(t => t.Value)) + string.Join("", mtexts.Select(t => t.Value));
            return combined.ToUpper();
        }

        private TopFeature AnalyzeZoneTop(IEnumerable<Entity> entities, BoundingBox rectBox)
        {
            double rectW = rectBox.Max.X - rectBox.Min.X;
            double rectH = rectBox.Max.Y - rectBox.Min.Y;
            double tolerance = Math.Min(Math.Max(Math.Min(rectW, rectH) * 0.2, 5.0), 20.0);
            double sideMargin = Math.Min(Math.Max(rectW * 0.3, 5.0), 30.0);
            double topSpan = Math.Min(Math.Max(rectH * 1.2, 10.0), 60.0);
            var topZone = new BoundingBox(
                new XYZ(rectBox.Min.X - sideMargin, rectBox.Max.Y - tolerance, 0),
                new XYZ(rectBox.Max.X + sideMargin, rectBox.Max.Y + topSpan, 0));
            
            var topEnts = entities.Where(e => Intersects(GetBoundingBox(e), topZone)).ToList();

            if (!topEnts.Any()) return TopFeature.None;

            var circles = topEnts.OfType<Circle>().ToList();
            var lines = topEnts.OfType<Line>().ToList();
            var verticals = lines.Where(IsVertical)
                .Where(l => LineExtendsAboveRect(l, rectBox, tolerance))
                .ToList();
            var horizontals = lines.Where(IsHorizontal)
                .Where(l => LineIsAboveRect(l, rectBox, tolerance))
                .ToList();
            
            // Check for CircleM
            if (circles.Any())
            {
                var midX = (rectBox.Min.X + rectBox.Max.X) / 2.0;
                double minDim = Math.Min(rectW, rectH);
                foreach (var c in circles)
                {
                    if (!IsInside(c.Center, topZone)) continue;
                    if (c.Radius < minDim * 0.06 || c.Radius > minDim * 0.45) continue;
                    if (Math.Abs(c.Center.X - midX) > rectW * 0.35) continue;

                    var zoneBox = new BoundingBox(c.Center - new XYZ(c.Radius, c.Radius,0), c.Center + new XYZ(c.Radius,c.Radius,0));
                    string t = GetTextInZone(topEnts, zoneBox, Zone.Center);
                    
                    bool hasM = t.Contains("M") || t.Contains("m");
                    bool connected = verticals.Any(l => Distance(l.EndPoint, c.Center) < tolerance || Distance(l.StartPoint, c.Center) < tolerance);
                     
                     if (connected && (t.Contains("3") || t.Contains("5"))) continue; 
                     if (connected || hasM) return TopFeature.CircleM; 
                }
            }
            
            // Check for TShape (Vertical Line + Horizontal Line) - Manual Valve (Centered)
            var midX2 = (rectBox.Min.X + rectBox.Max.X) / 2.0;
            // T-Shape tends to be centered
            var centerVert = verticals.FirstOrDefault(l => Math.Abs(l.StartPoint.X - midX2) < rectW * 0.25);
            
            if (centerVert != null)
            {
                var topPt = GetLineTopPoint(centerVert);
                bool hasHoriz = horizontals.Any(l => DistanceToLine(l, topPt) < tolerance);
                if (hasHoriz) return TopFeature.TShape;
            }

            // Check for LShape (Vertical + Horizontal/Box) - Gravity Relief Valve (Often Corner)
            // If we didn't match TShape, look for ANY vertical connected to a horizontal
            var anyVert = verticals.FirstOrDefault(l =>
                Math.Abs(l.StartPoint.X - rectBox.Min.X) < rectW * 0.35 ||
                Math.Abs(l.StartPoint.X - rectBox.Max.X) < rectW * 0.35);
            if (anyVert != null)
            {
                var topPt = GetLineTopPoint(anyVert);
                 // Check connectivity to a Horizontal line (The lever/arm)
                bool hasArm = horizontals.Any(l => DistanceToLine(l, topPt) < tolerance);
                if (hasArm) return TopFeature.LShape;
            }

            // Check for Zigzag
            if (topEnts.OfType<LwPolyline>().Any(p => IsZigZag(p))) return TopFeature.Zigzag;

            return TopFeature.None;
        }

        private bool IsVertical(Line l) => Math.Abs(l.StartPoint.X - l.EndPoint.X) < 1.0;
        private bool IsHorizontal(Line l) => Math.Abs(l.StartPoint.Y - l.EndPoint.Y) < 1.0;
        private double Distance(XYZ a, XYZ b) => Math.Sqrt(Math.Pow(a.X-b.X, 2) + Math.Pow(a.Y-b.Y, 2));
        private double DistanceToLine(Line l, XYZ p) => DistToSegment(p, l.StartPoint, l.EndPoint);

        private double Area(LwPolyline p)
        {
             var b = GetBoundingBox(p);
             return (b.Max.X - b.Min.X) * (b.Max.Y - b.Min.Y);
        }

        private XYZ GetLineTopPoint(Line l)
        {
            return l.EndPoint.Y >= l.StartPoint.Y ? l.EndPoint : l.StartPoint;
        }

        private bool LineExtendsAboveRect(Line l, BoundingBox rect, double tolerance)
        {
            double maxY = Math.Max(l.StartPoint.Y, l.EndPoint.Y);
            double minY = Math.Min(l.StartPoint.Y, l.EndPoint.Y);
            return maxY >= rect.Max.Y + tolerance * 0.3 &&
                   minY <= rect.Max.Y + tolerance;
        }

        private bool LineIsAboveRect(Line l, BoundingBox rect, double tolerance)
        {
            double minY = Math.Min(l.StartPoint.Y, l.EndPoint.Y);
            return minY >= rect.Max.Y - tolerance;
        }

        private bool IsRectanglePolyline(LwPolyline p)
        {
            if (!p.IsClosed || p.Vertices.Count < 4) return false;
            var b = GetBoundingBox(p);
            double w = b.Max.X - b.Min.X;
            double h = b.Max.Y - b.Min.Y;
            if (w <= 0 || h <= 0) return false;

            double tol = Math.Min(Math.Max(Math.Min(w, h) * 0.05, 1.0), 5.0);
            bool hasMinX = false;
            bool hasMaxX = false;
            bool hasMinY = false;
            bool hasMaxY = false;

            foreach (var v in p.Vertices)
            {
                double x = v.Location.X;
                double y = v.Location.Y;
                bool onMinX = Math.Abs(x - b.Min.X) <= tol;
                bool onMaxX = Math.Abs(x - b.Max.X) <= tol;
                bool onMinY = Math.Abs(y - b.Min.Y) <= tol;
                bool onMaxY = Math.Abs(y - b.Max.Y) <= tol;

                if (!(onMinX || onMaxX || onMinY || onMaxY)) return false;

                if (onMinX) hasMinX = true;
                if (onMaxX) hasMaxX = true;
                if (onMinY) hasMinY = true;
                if (onMaxY) hasMaxY = true;
            }

            return hasMinX && hasMaxX && hasMinY && hasMaxY;
        }

        private bool HasWShapeInRect(IEnumerable<Entity> entities, BoundingBox rect)
        {
            double rectW = rect.Max.X - rect.Min.X;
            double rectH = rect.Max.Y - rect.Min.Y;
            double minSpanW = rectW * 0.4;
            double minSpanH = rectH * 0.2;
            double maxSpanH = rectH * 0.9;

            foreach (var pl in entities.OfType<LwPolyline>())
            {
                if (pl.Vertices.Count < 4) continue;
                var b = GetBoundingBox(pl);
                if (!IsBoxInside(rect, b, 1.0)) continue;
                double w = b.Max.X - b.Min.X;
                double h = b.Max.Y - b.Min.Y;
                if (w < minSpanW || h < minSpanH || h > maxSpanH) continue;

                int? prevSign = null;
                int flips = 0;
                int cnt = pl.Vertices.Count;
                int segCount = pl.IsClosed ? cnt : cnt - 1;
                for (int i = 0; i < segCount; i++)
                {
                    var p1 = pl.Vertices[i].Location;
                    var p2 = pl.Vertices[(i + 1) % cnt].Location;
                    double dx = p2.X - p1.X;
                    double dy = p2.Y - p1.Y;
                    if (Math.Abs(dx) < 1.0 && Math.Abs(dy) < 1.0) continue;
                    int sign = Math.Sign(dy);
                    if (sign == 0) continue;
                    if (prevSign.HasValue && sign != prevSign.Value) flips++;
                    prevSign = sign;
                }

                if (flips >= 2) return true;
            }

            return false;
        }
        
        private bool HasManyLines(List<Entity> ents) => ents.OfType<Line>().Count() > 4;
        private Direction CheckArrowDirection(List<Entity> e, BoundingBox b) => Direction.Inward; // Placeholder
        private bool IsZigZag(LwPolyline p) => p.Vertices.Count > 4;

        private bool HasDiagonalInRect(IEnumerable<Entity> entities, BoundingBox rect)
        {
            double w = rect.Max.X - rect.Min.X;
            double h = rect.Max.Y - rect.Min.Y;
            double minLen = Math.Max(Math.Min(w, h) * 0.3, 5.0);

            foreach (var line in entities.OfType<Line>())
            {
                if (!IsInside(line.StartPoint, rect) || !IsInside(line.EndPoint, rect)) continue;
                if (IsHorizontal(line) || IsVertical(line)) continue;
                if (Distance(line.StartPoint, line.EndPoint) >= minLen) return true;
            }

            foreach (var pl in entities.OfType<LwPolyline>())
            {
                if (pl.Vertices.Count < 2) continue;
                int cnt = pl.Vertices.Count;
                int segCount = pl.IsClosed ? cnt : cnt - 1;
                for (int i = 0; i < segCount; i++)
                {
                    var p1 = pl.Vertices[i].Location;
                    var p2 = pl.Vertices[(i + 1) % cnt].Location;
                    var v1 = new XYZ(p1.X, p1.Y, 0);
                    var v2 = new XYZ(p2.X, p2.Y, 0);
                    if (!IsInside(v1, rect) || !IsInside(v2, rect)) continue;
                    if (Math.Abs(v1.X - v2.X) < 1.0 || Math.Abs(v1.Y - v2.Y) < 1.0) continue;
                    if (Distance(v1, v2) >= minLen) return true;
                }
            }

            return false;
        }

        private bool IsInside(XYZ pt, BoundingBox box)
        {
            return pt.X >= box.Min.X && pt.X <= box.Max.X &&
                   pt.Y >= box.Min.Y && pt.Y <= box.Max.Y;
        }

        private BoundingBox ExpandBox(BoundingBox box, double margin)
        {
            return new BoundingBox(
                new XYZ(box.Min.X - margin, box.Min.Y - margin, 0),
                new XYZ(box.Max.X + margin, box.Max.Y + margin, 0));
        }

        private bool IsBoxInside(BoundingBox outer, BoundingBox inner, double tolerance)
        {
            return inner.Min.X >= outer.Min.X - tolerance &&
                   inner.Min.Y >= outer.Min.Y - tolerance &&
                   inner.Max.X <= outer.Max.X + tolerance &&
                   inner.Max.Y <= outer.Max.Y + tolerance;
        }

        private bool IsTopAdjacent(BoundingBox rect, BoundingBox other, double maxGap, double sideTol)
        {
            double verticalGap = other.Min.Y - rect.Max.Y;
            if (verticalGap > maxGap) return false;
            if (other.Max.Y < rect.Max.Y - maxGap) return false;

            bool xOverlap = other.Max.X >= rect.Min.X - sideTol &&
                            other.Min.X <= rect.Max.X + sideTol;
            return xOverlap;
        }

        private bool TryGetTextInsertPoint(Entity e, out XYZ point)
        {
            switch (e)
            {
                case TextEntity text:
                    point = text.InsertPoint;
                    return true;
                case MText mtext:
                    point = mtext.InsertPoint;
                    return true;
                default:
                    point = XYZ.Zero;
                    return false;
            }
        }

        private BoundingBox GetBoundingBox(Entity e)
        {
            try 
            {
                var bbox = e.GetBoundingBox();
                if(bbox.Min.X <= bbox.Max.X) return bbox;
            }
            catch { }

            if (e is LwPolyline pl)
            {
                if(pl.Vertices.Count == 0) return new BoundingBox(XYZ.Zero, XYZ.Zero);
                double minx = pl.Vertices.Min(v => v.Location.X);
                double miny = pl.Vertices.Min(v => v.Location.Y);
                double maxx = pl.Vertices.Max(v => v.Location.X);
                double maxy = pl.Vertices.Max(v => v.Location.Y);
                return new BoundingBox(new XYZ(minx, miny, 0), new XYZ(maxx, maxy, 0));
            }
            return new BoundingBox(XYZ.Zero, XYZ.Zero);
        }

        private BoundingBox GetBoundingBox(List<Entity> entities)
        {
            if(!entities.Any()) return new BoundingBox(new XYZ(0,0,0), new XYZ(0,0,0));
            
            double minx = double.MaxValue, miny = double.MaxValue;
            double maxx = double.MinValue, maxy = double.MinValue;
            bool valid = false;

            foreach(var e in entities)
            {
                var b = GetBoundingBox(e);
                if (b.Max.X < b.Min.X) continue; 
                valid = true;
                if(b.Min.X < minx) minx = b.Min.X;
                if(b.Min.Y < miny) miny = b.Min.Y;
                if(b.Max.X > maxx) maxx = b.Max.X;
                if(b.Max.Y > maxy) maxy = b.Max.Y;
            }
            if(!valid) return new BoundingBox(new XYZ(0,0,0), new XYZ(0,0,0));
             return new BoundingBox(new XYZ(minx, miny, 0), new XYZ(maxx, maxy, 0));
        }

        private bool Intersects(BoundingBox a, BoundingBox b)
        {
             return (a.Min.X <= b.Max.X && a.Max.X >= b.Min.X) &&
                    (a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y);
        }
    }

    public enum Zone { Top, Center, Side }
    public enum TopFeature { None, CircleM, TShape, LShape, Zigzag }
    public enum Direction { Unknown, Inward, Outward }
}
