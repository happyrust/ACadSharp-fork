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
        public List<LegendGroup> Groups { get; private set; } = new List<LegendGroup>();

        public List<Entity> FrameEntities { get; private set; } = new List<Entity>();


        private void IdentifyAndExcludeFrames(List<Entity> entities)
        {
            FrameEntities.Clear();
            var toRemove = new List<Entity>();

            foreach(var e in entities)
            {
                var bbox = GetBoundingBox(e);
                double w = bbox.Max.X - bbox.Min.X;
                double h = bbox.Max.Y - bbox.Min.Y;

                // Heuristic: Extremely large size (likely frame border)
                // NOW HANDLED BY DYNAMIC CLUSTERING (See Step 3 in RunNew)
                /*
                if (w > 300 || h > 300)
                {
                    toRemove.Add(e);
                    continue;
                }
                */
            }

            foreach(var e in toRemove)
            {
                FrameEntities.Add(e);
                entities.Remove(e);
            }
        }

        public void RunNew(IEnumerable<Entity> entities)
        {
            var allEntities = entities.ToList();
            
            // 0. Exclude Frames
            IdentifyAndExcludeFrames(allEntities);
            
            // 1. Pre-filter (Exclude text/dims from clustering candidates, but keep for context)
            var preFiltered = allEntities.Where(e => 
                !(e is TextEntity) && 
                !(e is MText) && 
                !(e is Dimension) &&
                !(e is Hatch) // Exclude hatches from primary shape
            ).ToList();

            // 1.1. Calculate Grid/Frame extent (using the remaining entities)
            var totalBox = GetBoundingBox(preFiltered);
            double totalW = totalBox.Max.X - totalBox.Min.X;
            double totalH = totalBox.Max.Y - totalBox.Min.Y;
            
            // 2. Filter Grid Lines (Long Horizontal/Vertical lines and Noise)
            // Remove anything > 50% of total dimension OR too small
            double maxLenW = totalW * 0.5;
            double maxLenH = totalH * 0.5;
            
            // Also enforce absolute max (e.g. 2000) just in case
            maxLenW = Math.Min(maxLenW, 2000.0);
            maxLenH = Math.Min(maxLenH, 2000.0);

            // Re-assign to filtered list
            preFiltered = preFiltered.Where(e =>
            {
                 var b = GetBoundingBox(e);
                 double w = b.Max.X - b.Min.X;
                 double h = b.Max.Y - b.Min.Y;
                 if (w > maxLenW || h > maxLenH) return false;
                 
                 // Noise filter
                 if (e is Line l && Distance(l.StartPoint, l.EndPoint) < 3.0) return false;
                 if (e is Circle c && c.Radius * 2.0 < 3.0) return false;
                 
                 return true;
            }).ToList();

            // 3. Cluster by Bounding Box (Proximity)
            // Use strict tolerance.
            var rawClusters = ClusterByBoundingBoxes(preFiltered, 5.0);
            
            // Set RawClusters for debug
            RawClusters = rawClusters.Select(c => new LegendCandidate
            {
                Entities = c.Entities,
                BoundingBox = c.BoundingBox
            }).ToList();
            
            // Dynamic Frame Exclusion (Outlier Detection)
            if (RawClusters.Any())
            {
                // Calculate diagonal lengths
                var diagonals = RawClusters.Select(c => 
                {
                    double w = c.BoundingBox.Max.X - c.BoundingBox.Min.X;
                    double h = c.BoundingBox.Max.Y - c.BoundingBox.Min.Y;
                    return Math.Sqrt(w*w + h*h);
                }).OrderBy(x => x).ToList();

                // Calculate Median
                double median = 0;
                int count = diagonals.Count;
                if (count > 0)
                {
                    if (count % 2 == 0) median = (diagonals[count/2 - 1] + diagonals[count/2]) / 2.0;
                    else median = diagonals[count/2];
                }

                // Threshold: If Median is very small (e.g. < 1), might be noise. 
                // Assumed valid legend size > 5.
                median = Math.Max(median, 5.0);

                // Define Outlier Threshold (e.g. 10x Median)
                double threshold = median * 10.0;
                
                Console.WriteLine($"[FrameExclusion] Median Size: {median:F2}, Threshold: {threshold:F2}");

                Candidates = RawClusters.Where(c => 
                {
                    double w = c.BoundingBox.Max.X - c.BoundingBox.Min.X;
                    double h = c.BoundingBox.Max.Y - c.BoundingBox.Min.Y;
                    double diag = Math.Sqrt(w*w + h*h);
                    
                    if (diag > threshold)
                    {
                        Console.WriteLine($"[FrameExclusion] Excluding Cluster Size: {w:F1}x{h:F1} (Diag: {diag:F1})");
                        return false;
                    }
                    return true;
                }).ToList();
            }
            else
            {
                Candidates = new List<LegendCandidate>();
            }

            // 4. Attach Context (Labels)
            AttachContextEntities(Candidates, allEntities);

            // 5. Identification
            int candidateIndex = 1;
            foreach (var candidate in Candidates)
            {
                candidate.DetectedType = IdentifyLegendNew(candidate);
                candidate.Index = candidateIndex++;
            }

            // 6. Grouping
            Groups = GroupCandidatesByProximity(Candidates, 5.0);
        }

        private List<LegendGroup> GroupCandidatesByProximity(List<LegendCandidate> candidates, double tolerance)
        {
            int n = candidates.Count;
            if (n == 0) return new List<LegendGroup>();

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
                var bi = candidates[i].BoundingBox;
                for (int j = i + 1; j < n; j++)
                {
                    var bj = candidates[j].BoundingBox;
                    if (Intersects(bi, bj) || BoxesAreNear(bi, bj, tolerance) ||
                        IsBoxInside(bi, bj, tolerance) || IsBoxInside(bj, bi, tolerance))
                    {
                        Unite(i, j);
                    }
                }
            }

            var groupMap = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (!groupMap.ContainsKey(r)) groupMap[r] = new List<int>();
                groupMap[r].Add(i);
            }

            var result = new List<LegendGroup>();
            int groupIndex = 1;
            foreach (var indices in groupMap.Values)
            {
                var members = indices.Select(idx => candidates[idx]).ToList();
                var combinedBox = GetBoundingBox(members.SelectMany(m => m.Entities).ToList());
                result.Add(new LegendGroup
                {
                    Index = groupIndex++,
                    Members = members,
                    BoundingBox = combinedBox
                });
            }

            return result;
        }

        public void Run(IEnumerable<Entity> entities)
        {
            // Redirect to New Logic
            RunNew(entities);
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
                    if (rectBoxes[j] == null) continue;
                    var otherBox = rectBoxes[j]!.Value;

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

        private bool IsGreenOrNearGreen(Entity e)
        {
            var color = e.GetActiveColor();
            if (color.IsTrueColor)
            {
                int idx = color.GetApproxIndex();
                return idx == 3 || idx == 2 || idx == 4;
            }
            return color.Index == 3 || color.Index == 2 || color.Index == 4;
        }

        private List<Entity> FilterNoiseAndFrames(List<Entity> entities, double maxDimension)
        {
             return entities.Where(e =>
            {
                // Check Size
                var b = GetBoundingBox(e);
                double w = b.Max.X - b.Min.X;
                double h = b.Max.Y - b.Min.Y;
                if (w > maxDimension || h > maxDimension) return false;

                switch (e)
                {
                    case Line l:
                        return Distance(l.StartPoint, l.EndPoint) > 3.0;
                    case Circle c:
                        return c.Radius * 2.0 > 3.0;
                    case LwPolyline p:
                        return p.Vertices.Count >= 2;
                    default:
                        return true;
                }
            }).ToList();
        }

        private List<Entity> FilterNoise(List<Entity> entities)
        {
            return FilterNoiseAndFrames(entities, double.MaxValue);
        }

		private enum ShapeKind { Rect, Triangle, Circle }

		private class ShapeFeature
		{
			public ShapeKind Kind { get; set; }
			public BoundingBox Box { get; set; }
			public HashSet<Entity> Entities { get; set; } = new HashSet<Entity>();
		}

		private List<ShapeFeature> ExtractShapeFeatures(IEnumerable<Entity> entities)
		{
			var shapes = new List<ShapeFeature>();
			double minSize = 3.0;
			double maxSize = 50000.0;

            foreach (var c in entities.OfType<Circle>())
            {
                double d = c.Radius * 2.0;
                if (d < minSize || d > maxSize) continue;
                var box = new BoundingBox(
                    new XYZ(c.Center.X - c.Radius, c.Center.Y - c.Radius, 0),
                    new XYZ(c.Center.X + c.Radius, c.Center.Y + c.Radius, 0));
                shapes.Add(new ShapeFeature { Kind = ShapeKind.Circle, Box = box, Entities = new HashSet<Entity> { c } });
            }

            foreach (var pl in entities.OfType<LwPolyline>().Where(p => p.IsClosed))
            {
                var box = GetBoundingBox(pl);
                double w = box.Max.X - box.Min.X;
                double h = box.Max.Y - box.Min.Y;
                double diag = Math.Sqrt(w * w + h * h);
                if (w < minSize || h < minSize || diag > maxSize) continue;

                if (IsRectanglePolyline(pl))
                {
                    shapes.Add(new ShapeFeature { Kind = ShapeKind.Rect, Box = box, Entities = new HashSet<Entity> { pl } });
                    continue;
                }

                if (pl.Vertices.Count == 3)
                {
                    shapes.Add(new ShapeFeature { Kind = ShapeKind.Triangle, Box = box, Entities = new HashSet<Entity> { pl } });
                }
            }

			return shapes;
		}

		// BBOX 聚类：基于实体 BBOX 的相交/贴近/包含合并
        private List<LegendCandidate> ClusterByBoundingBoxes(List<Entity> entities, double fixedTolerance)
        {
            var boxes = new List<(BoundingBox box, Entity ent)>();
            foreach (var e in entities)
            {
                var b = GetBoundingBox(e);
                if (b.Max.X < b.Min.X || b.Max.Y < b.Min.Y) continue;
                double w = b.Max.X - b.Min.X;
                double h = b.Max.Y - b.Min.Y;
                if (w < 1.0 && h < 1.0) continue;
                boxes.Add((b, e));
            }

            int n = boxes.Count;
            if (n == 0) return new List<LegendCandidate>();

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
                var a = boxes[i].box;
                for (int j = i + 1; j < n; j++)
                {
                    var b = boxes[j].box;
                    if (Intersects(a, b) || BoxesAreNear(a, b, fixedTolerance) ||
                        IsBoxInside(a, b, fixedTolerance) || IsBoxInside(b, a, fixedTolerance))
                    {
                        Unite(i, j);
                    }
                }
            }

            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (!groups.ContainsKey(r)) groups[r] = new List<int>();
                groups[r].Add(i);
            }

            var result = new List<LegendCandidate>();
            foreach (var g in groups.Values)
            {
                var ents = new HashSet<Entity>();
                foreach (var idx in g)
                {
                    ents.Add(boxes[idx].ent);
                }
                var box = GetBoundingBox(ents.ToList());
                result.Add(new LegendCandidate
                {
                    Entities = ents.ToList(),
                    BoundingBox = box
                });
            }

            return result;
        }

		private List<LegendCandidate> ClusterByBoundingBoxes(List<Entity> entities)
		{
			var boxes = new List<(BoundingBox box, Entity ent)>();
			foreach (var e in entities)
			{
				var b = GetBoundingBox(e);
				if (b.Max.X < b.Min.X || b.Max.Y < b.Min.Y) continue;
				double w = b.Max.X - b.Min.X;
				double h = b.Max.Y - b.Min.Y;
				if (w < 1.0 && h < 1.0) continue;
				boxes.Add((b, e));
			}

			int n = boxes.Count;
			if (n == 0) return new List<LegendCandidate>();

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
				var a = boxes[i].box;
				double minDimA = Math.Min(a.Max.X - a.Min.X, a.Max.Y - a.Min.Y);
				double tolA = Math.Min(Math.Max(minDimA * 0.2, 1.0), 20.0);
				for (int j = i + 1; j < n; j++)
				{
					var b = boxes[j].box;
					double minDimB = Math.Min(b.Max.X - b.Min.X, b.Max.Y - b.Min.Y);
					double tol = Math.Min(Math.Max(Math.Min(minDimA, minDimB) * 0.2, 1.0), 20.0);

					if (Intersects(a, b) || BoxesAreNear(a, b, tol) ||
						IsBoxInside(a, b, tol) || IsBoxInside(b, a, tol))
					{
						Unite(i, j);
					}
				}
			}

			var groups = new Dictionary<int, List<int>>();
			for (int i = 0; i < n; i++)
			{
				int r = Find(i);
				if (!groups.ContainsKey(r)) groups[r] = new List<int>();
				groups[r].Add(i);
			}

			var result = new List<LegendCandidate>();
			foreach (var g in groups.Values)
			{
				var ents = new HashSet<Entity>();
				foreach (var idx in g)
				{
					ents.Add(boxes[idx].ent);
				}
				var box = GetBoundingBox(ents.ToList());
				result.Add(new LegendCandidate
				{
					Entities = ents.ToList(),
					BoundingBox = box
				});
			}

			return result;
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

        private LegendType IdentifyLegendNew(LegendCandidate candidate)
        {
            var contextEntities = GetContextEntities(candidate);
            var rect = FindMainRectangle(candidate);
            candidate.AnchorRect = rect;

            var bbox = rect != null ? GetBoundingBox(rect) : candidate.BoundingBox;
            string text = GetTextInZone(contextEntities, bbox, Zone.Center);
            
            // 0. Check Block Names (Inserts)
            foreach (var ent in candidate.Entities.OfType<Insert>())
            {
                string blkName = ent.Block.Name.ToUpper();
                if (blkName.Contains("GQ") || blkName.Contains("FAN")) return LegendType.Fan;
                if (blkName.Contains("GZ") || blkName.Contains("COOLING")) return LegendType.DirectCoolingUnit;
                if (blkName.Contains("SPLIT")) return LegendType.SplitUnit;
            }

            // 1. Check Fan (No Rect)
            if (rect == null)
            {
                if (IsFan(candidate.Entities, candidate.BoundingBox)) return LegendType.Fan;
            }

            // 2. Check Split Unit (Two Rects linked)
            if (IsSplitUnit(candidate)) return LegendType.SplitUnit;

            // 3. Rect based
            if (rect != null)
            {
                var ents = candidate.Entities;
                // Pre-calculate expensive checks
                bool hasFan = HasFanInRect(ents, bbox);
                bool hasW = HasWShapeInRect(ents, bbox);
                bool hasM = text.Contains("M") || HasMShapeInRect(ents, bbox);
                
                if ((hasFan || HasAnyCircleInRect(ents, bbox)) && hasM) return LegendType.DirectCoolingUnit;
                if (hasFan && hasW) return LegendType.CoolingUnit; // Simplified check
                
                if (TryDetectUnit(candidate, bbox, text, out var unitType)) return unitType;
        // IsSilencer moved down
        if (IsAirCurtain(candidate, bbox, contextEntities)) return LegendType.AirCurtain;
        
        // Existing Checks
        bool hasDiagonal = HasDiagonalInRect(candidate.Entities, bbox);
        bool hasWShape = HasWShapeInRect(candidate.Entities, bbox);
        
        if (text.Contains("F")) return LegendType.FireDamper;
        if (text.Contains("E")) return LegendType.SmokeFireDamper;
        
        // Check Silencer after F/E but before Louver
        if (IsSilencer(candidate, bbox)) return LegendType.Silencer;

        var topFeature = AnalyzeZoneTop(contextEntities, bbox);
                if (topFeature == TopFeature.CircleM) return LegendType.ElectricIsolationValve;
                if (topFeature == TopFeature.TShape) return LegendType.ManualIsolationValve;
                if (topFeature == TopFeature.LShape) return LegendType.GravityReliefValve;
                if (topFeature == TopFeature.Zigzag) return LegendType.BlastValve;

                double w = bbox.Max.X - bbox.Min.X;
                double h = bbox.Max.Y - bbox.Min.Y;
                double ratio = w > 0 ? h / w : 1.0; 
                if (w > h) ratio = w / h; // Just max/min ratio

                if (ratio > 2.5)
                {
                     var dir = CheckArrowDirection(candidate.Entities, bbox);
                     if (dir == Direction.Inward) return LegendType.FreshAirLouver;
                     if (dir == Direction.Outward) return LegendType.ExhaustLouver;
                     if (HasManyLines(candidate.Entities)) return LegendType.FreshAirLouver;
                }
                
                if (hasWShape) return LegendType.BlastValve;
                if (hasDiagonal) return LegendType.CheckValve;
            }
            
            return LegendType.Unknown;
        }

        private bool IsFan(List<Entity> entities, BoundingBox box)
        {
            // Find a large circle
            var circles = entities.OfType<Circle>().OrderByDescending(c => c.Radius).ToList();
            if (!circles.Any()) return false;
            
            // 1. Precise Geometry Check
            if (circles.Any(c => IsFanLike(c, entities))) return true;
            
            // 2. Heuristic Fallback: 1 Circle + >= 3 Lines + Size constraint
            if (circles.Count == 1 && entities.OfType<Line>().Count() >= 3)
            {
                double w = box.Max.X - box.Min.X;
                double h = box.Max.Y - box.Min.Y;
                if (w > 5 && w < 50 && h > 5 && h < 50) return true;
            }
            
            return false;
        }

        private bool HasFanInRect(IEnumerable<Entity> entities, BoundingBox rect)
        {
            double minDim = Math.Min(rect.Max.X - rect.Min.X, rect.Max.Y - rect.Min.Y);
            double tol = Math.Min(Math.Max(minDim * 0.05, 1.0), 10.0);
            
            foreach (var c in entities.OfType<Circle>())
            {
                var cbox = new BoundingBox(
                    new XYZ(c.Center.X - c.Radius, c.Center.Y - c.Radius, 0),
                    new XYZ(c.Center.X + c.Radius, c.Center.Y + c.Radius, 0));
                
                if (!IsBoxInside(rect, cbox, tol)) continue;
                if (IsFanLike(c, entities)) return true;
            }
            return false;
        }

        private bool HasAnyCircleInRect(IEnumerable<Entity> entities, BoundingBox rect)
        {
            return entities.OfType<Circle>().Any(c => IsInside(c.Center, rect));
        }

        private bool IsFanLike(Circle circle, IEnumerable<Entity> context)
        {
            double r = circle.Radius;
            double centerTol = Math.Max(r * 0.4, 3.0); // 40% or 3.0 units
            
            int validLines = 0;
            
            foreach(var e in context)
            {
                if (e is Line l)
                {
                    // Relaxed containment: 1.8x radius (lines can stick out)
                    bool startIn = Distance(l.StartPoint, circle.Center) <= r * 1.8;
                    bool endIn = Distance(l.EndPoint, circle.Center) <= r * 1.8;
                    
                    if (startIn && endIn)
                    {
                        if (Distance(l.StartPoint, circle.Center) < centerTol || 
                            Distance(l.EndPoint, circle.Center) < centerTol)
                        {
                            validLines++;
                        }
                        else if (DistToSegment(circle.Center, l.StartPoint, l.EndPoint) < centerTol)
                        {
                            validLines++;
                        }
                    }
                }
            }
            
            return validLines >= 3;
        }

        private bool HasMShapeInRect(IEnumerable<Entity> entities, BoundingBox rect)
        {
             // 1. Try generic ZigZag (Polyline)
             if (HasWShapeInRect(entities, rect)) return true;
             
             // 2. Try M made of lines (4 lines connected or close)
             // Look for 2 vertical-ish lines and 2 diagonal lines?
             var lines = entities.OfType<Line>().Where(l => IsInside(l.StartPoint, rect) && IsInside(l.EndPoint, rect)).ToList();
             if (lines.Count < 4) return false;
             
             // Simple heuristic: 2 verticals + 2 diagonals
             int verts = lines.Count(l => IsVertical(l));
             int diags = lines.Count(l => !IsVertical(l) && !IsHorizontal(l));
             
             if (verts >= 2 && diags >= 2) return true;
             
             return false;
        }
        
        private bool IsSplitUnit(LegendCandidate candidate)
        {
            var rects = GetRectangles(candidate).OrderByDescending(Area).ToList();
            if (rects.Count < 2) return false;
            
            var r1 = rects[0];
            var r2 = rects[1]; // Largest two
            
            // Check if connected by a line
            // Or just check if they exist and are roughly aligned vertically
            var b1 = GetBoundingBox(r1);
            var b2 = GetBoundingBox(r2);
            
            // Look for lines that connect the two boxes
            var lines = candidate.Entities.OfType<Line>();
            foreach(var l in lines)
            {
                 bool touch1 = IsInside(l.StartPoint, ExpandBox(b1, 5.0)) || IsInside(l.EndPoint, ExpandBox(b1, 5.0));
                 bool touch2 = IsInside(l.StartPoint, ExpandBox(b2, 5.0)) || IsInside(l.EndPoint, ExpandBox(b2, 5.0));
                 if (touch1 && touch2) return true;
            }
            
            // Check for polyline connection
             var plines = candidate.Entities.OfType<LwPolyline>();
             foreach(var p in plines)
             {
                 if (p == r1 || p == r2) continue;
                 bool touch1 = p.Vertices.Any(v => IsInside(new XYZ(v.Location.X, v.Location.Y, 0), ExpandBox(b1, 5.0)));
                 bool touch2 = p.Vertices.Any(v => IsInside(new XYZ(v.Location.X, v.Location.Y, 0), ExpandBox(b2, 5.0)));
                 if (touch1 && touch2) return true;
             }
             
             return false;
        }

        private LegendType IdentifyLegend(LegendCandidate candidate)
        {
             return IdentifyLegendNew(candidate);
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
            if (lines.Count >= 4 && lines.Count <= 200)
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

        public bool IsRectanglePolyline(LwPolyline p)
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
        
        private bool TryDetectUnit(LegendCandidate candidate, BoundingBox anchor, string centerText, out LegendType type)
        {
            type = LegendType.Unknown;
            bool fan = HasFanInRect(candidate.Entities, anchor);
            bool hasW = centerText.Contains("W") || HasWShapeInRect(candidate.Entities, anchor);
            int diamonds = CountDiamondsInRect(candidate.Entities, anchor);
            if (fan && hasW)
            {
                if (diamonds >= 2)
                {
                    type = LegendType.AirHandlingUnit;
                    return true;
                }
                type = LegendType.CoolingUnit;
                return true;
            }
            if (fan && diamonds >= 2)
            {
                type = LegendType.AirHandlingUnit;
                return true;
            }
            return false;
        }
        


        private int CountDiamondsInRect(IEnumerable<Entity> entities, BoundingBox rect)
        {
            int count = 0;
            double tol = Math.Min(Math.Max(Math.Min(rect.Max.X - rect.Min.X, rect.Max.Y - rect.Min.Y) * 0.05, 1.0), 8.0);
            foreach (var pl in entities.OfType<LwPolyline>())
            {
                if (!LooksLikeDiamond(pl, rect, tol)) continue;
                count++;
            }
            return count;
        }

        private bool LooksLikeDiamond(LwPolyline pl, BoundingBox rect, double tol)
        {
            if (!pl.IsClosed || pl.Vertices.Count < 4) return false;
            var b = GetBoundingBox(pl);
            if (!IsBoxInside(rect, b, tol)) return false;
            double w = b.Max.X - b.Min.X;
            double h = b.Max.Y - b.Min.Y;
            if (w <= 0 || h <= 0) return false;
            double ratio = w / h;
            if (ratio < 0.5 || ratio > 1.5) return false;
            int diagEdges = 0;
            int cnt = pl.Vertices.Count;
            int segCount = pl.IsClosed ? cnt : cnt - 1;
            double minDiag = Math.Min(w, h) * 0.25;
            for (int i = 0; i < segCount; i++)
            {
                var p1 = pl.Vertices[i].Location;
                var p2 = pl.Vertices[(i + 1) % cnt].Location;
                double dx = Math.Abs(p2.X - p1.X);
                double dy = Math.Abs(p2.Y - p1.Y);
                if (dx < minDiag || dy < minDiag) continue;
                diagEdges++;
            }
            return diagEdges >= 3;
        }

        private bool IsSilencer(LegendCandidate candidate, BoundingBox anchor)
        {
            // 1. Text Context Check
            var context = candidate.ContextEntities;
            bool hasText = context.OfType<TextEntity>().Any(t => t.Value.Contains("RP") || t.Value.Contains("消声器") || t.Value.Contains("SILENCER")) 
                        || context.OfType<MText>().Any(t => t.Value.Contains("RP") || t.Value.Contains("消声器") || t.Value.Contains("SILENCER"));
            
            if (hasText) return true;

            // 2. Geometric Check (Relaxed)
            double w = anchor.Max.X - anchor.Min.X;
            double h = anchor.Max.Y - anchor.Min.Y;
            
            // Allow lines to be slightly outside (1.1x tolerance)
            BoundingBox expanded = new BoundingBox(
                new XYZ(anchor.Min.X - w*0.1, anchor.Min.Y - h*0.1, 0),
                new XYZ(anchor.Max.X + w*0.1, anchor.Max.Y + h*0.1, 0)
            );

            var verticals = candidate.Entities.OfType<Line>()
                .Where(IsVertical)
                .Where(l => IsInside(l.StartPoint, expanded) && IsInside(l.EndPoint, expanded))
                .ToList();

            // Count distinct X positions (baffles) using standard LINQ
            int baffles = verticals.Select(l => l.StartPoint.X)
                                   .Select(x => Math.Round(x / 2.0))
                                   .Distinct()
                                   .Count();

            // Relaxed from 3 to 2, and use distinct X count (handles segmented lines)
            return baffles >= 2 && RatioWithin(w, h, 0.7, 4.0);
        }

        private bool IsSplitCabinet(LegendCandidate candidate)
        {
            var rects = GetRectangles(candidate).OrderByDescending(Area).ToList();
            if (rects.Count < 2) return false;
            var r1 = rects[0];
            var r2 = rects[1];
            var b1 = GetBoundingBox(r1);
            var b2 = GetBoundingBox(r2);
            double cxDiff = Math.Abs((b1.Min.X + b1.Max.X) * 0.5 - (b2.Min.X + b2.Max.X) * 0.5);
            double maxW = Math.Max(b1.Max.X - b1.Min.X, b2.Max.X - b2.Min.X);
            if (cxDiff > maxW * 0.4) return false;
            double hRatio = (b1.Max.Y - b1.Min.Y) / Math.Max((b2.Max.Y - b2.Min.Y), 0.001);
            if (hRatio < 0.7 || hRatio > 1.4) return false;
            double gapY = Math.Abs((b1.Min.Y + b1.Max.Y) * 0.5 - (b2.Min.Y + b2.Max.Y) * 0.5);
            double minH = Math.Min(b1.Max.Y - b1.Min.Y, b2.Max.Y - b2.Min.Y);
            if (gapY < minH * 0.3) return false;
            bool r1Diag = HasDiagonalInRect(candidate.Entities, b1);
            bool r2Diag = HasDiagonalInRect(candidate.Entities, b2);
            return r1Diag && r2Diag;
        }

        private IEnumerable<LwPolyline> GetRectangles(LegendCandidate candidate)
        {
            foreach (var pl in candidate.Entities.OfType<LwPolyline>())
            {
                if (!pl.IsClosed) continue;
                if (IsRectanglePolyline(pl)) yield return pl;
            }
        }

        private List<LegendCandidate> MergeByBoundingBoxes(List<LegendCandidate> candidates, double tolerance)
        {
            int n = candidates.Count;
            if (n <= 1) return candidates;

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
                var boxA = candidates[i].BoundingBox;
                for (int j = i + 1; j < n; j++)
                {
                    var boxB = candidates[j].BoundingBox;
                    if (Intersects(boxA, boxB) || BoxesAreNear(boxA, boxB, tolerance) ||
                        IsBoxInside(boxA, boxB, tolerance) || IsBoxInside(boxB, boxA, tolerance))
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
            foreach (var group in merged.Values)
            {
                if (group.Count == 1)
                {
                    result.Add(group[0]);
                    continue;
                }
                var ents = group.SelectMany(g => g.Entities).ToList();
                var box = GetBoundingBox(ents);
                result.Add(new LegendCandidate
                {
                    Entities = ents,
                    BoundingBox = box
                });
            }

            return result;
        }

        public void EnrichGroups(IEnumerable<Entity> allEntities)
        {
             // Build a spatial index or just brute force for now (N groups * M entities)
             // Optimization: only consider entities not already in a group?
             // Or simpler: Just find everything inside BBOX.
             
             // Gather all entities currently in groups to avoid duplicates
             var processedEntities = new HashSet<Entity>();
             foreach(var g in Groups)
             {
                 foreach(var m in g.Members)
                 {
                     foreach(var e in m.Entities) processedEntities.Add(e);
                 }
             }

             // Iterate all entities
             foreach(var entity in allEntities)
             {
                 if (processedEntities.Contains(entity)) continue;
                 
                 // Check bounding box
                 var bbox = GetBoundingBox(entity);
                 if (bbox.Min.X == 0 && bbox.Min.Y == 0 && bbox.Max.X == 0 && bbox.Max.Y == 0) continue; // Skip empty/invalid

                 foreach(var g in Groups)
                 {
                     // Check if entity is strictly inside group BBOX (or close enough)
                     if (IsBoxInside(g.BoundingBox, bbox, 0.1)) 
                     {
                         g.AdditionalEntities.Add(entity);
                         processedEntities.Add(entity); 
                         
                         // Update Group BoundingBox to include this new entity
                         double minX = Math.Min(g.BoundingBox.Min.X, bbox.Min.X);
                         double minY = Math.Min(g.BoundingBox.Min.Y, bbox.Min.Y);
                         double maxX = Math.Max(g.BoundingBox.Max.X, bbox.Max.X);
                         double maxY = Math.Max(g.BoundingBox.Max.Y, bbox.Max.Y);
                         g.BoundingBox = new BoundingBox(new XYZ(minX, minY, 0), new XYZ(maxX, maxY, 0));
                         
                         break; 
                     }
                 }
             }
        }

        public static BoundingBox GetBoundingBox(Entity entity)
        {
            // Handle LwPolyline explicitly if ACadSharp doesn't do it right
            if (entity is LwPolyline pl)
            {
                if (pl.Vertices.Count == 0) return new BoundingBox(XYZ.Zero, XYZ.Zero);
                double minX = pl.Vertices.Min(v => v.Location.X);
                double minY = pl.Vertices.Min(v => v.Location.Y);
                double maxX = pl.Vertices.Max(v => v.Location.X);
                double maxY = pl.Vertices.Max(v => v.Location.Y);
                return new BoundingBox(new XYZ(minX, minY, 0), new XYZ(maxX, maxY, 0));
            }
            // Fallback to default
            BoundingBox? bbox = null;
            try { bbox = entity.GetBoundingBox(); } catch {}
            
            if (bbox.HasValue) return bbox.Value;
            
            // Try to calculate manually for common types if BoundingBox is null
            if (entity is Line l)
            {
                 double minX = Math.Min(l.StartPoint.X, l.EndPoint.X);
                 double minY = Math.Min(l.StartPoint.Y, l.EndPoint.Y);
                 double maxX = Math.Max(l.StartPoint.X, l.EndPoint.X);
                 double maxY = Math.Max(l.StartPoint.Y, l.EndPoint.Y);
                 return new BoundingBox(new XYZ(minX, minY, 0), new XYZ(maxX, maxY, 0));
            }
            if (entity is Circle c)
            {
                 return new BoundingBox(
                     new XYZ(c.Center.X - c.Radius, c.Center.Y - c.Radius, 0),
                     new XYZ(c.Center.X + c.Radius, c.Center.Y + c.Radius, 0));
            }

            return bbox ?? new BoundingBox(XYZ.Zero, XYZ.Zero);
        }

        public class ArrowInfo
        {
            public XYZ Tip { get; set; }
            public XYZ Direction { get; set; }
        }

        private bool IsAirCurtain(LegendCandidate candidate, BoundingBox anchor, IEnumerable<Entity> context)
        {
            double w = anchor.Max.X - anchor.Min.X;
            double h = anchor.Max.Y - anchor.Min.Y;
            if (h <= 0 || w <= 0) return false;
            double ratio = Math.Max(w, h) / Math.Min(w, h);
            if (ratio < 3.0) return false; // 竖长矩形

            // 查找箭头集合
            var arrows = DetectArrows(candidate.Entities, anchor);
            if (arrows.Count < 3) return false;

            // 箭头方向一致，且平均方向指向矩形外侧（假设矩形在左，箭头指向右）
            var dirAvg = new XYZ(arrows.Average(a => a.Direction.X), arrows.Average(a => a.Direction.Y), 0);
            double dirLen = Math.Sqrt(dirAvg.X * dirAvg.X + dirAvg.Y * dirAvg.Y);
            if (dirLen < 1e-3) return false;
            dirAvg = new XYZ(dirAvg.X / dirLen, dirAvg.Y / dirLen, 0);

            // 取矩形右侧包络
            double sideMinX = anchor.Max.X;
            double sideMaxX = anchor.Max.X + Math.Max(w * 1.5, 10.0);
            double yMin = anchor.Min.Y;
            double yMax = anchor.Max.Y;

            int onRight = arrows.Count(a => a.Tip.X >= sideMinX - 1.0 && a.Tip.X <= sideMaxX && a.Tip.Y >= yMin - 5 && a.Tip.Y <= yMax + 5);
            if (onRight < 3) return false;

            // 方向需朝右侧（X 正向）
            if (dirAvg.X < 0.2) return false;

            // 文本辅助：若附近文本含“热风幕”或“RQ”则直接确认
            string t = GetTextInZone(context, ExpandBox(anchor, Math.Max(w, h)), Zone.Center);
            if (t.Contains("热风幕") || t.Contains("RQ") || t.Contains("R Q")) return true;

            return true;
        }

        public List<ArrowInfo> InvokeDetectArrows(IEnumerable<Entity> entities, BoundingBox rect)
        {
            return DetectArrows(entities, rect);
        }

        private List<ArrowInfo> DetectArrows(IEnumerable<Entity> entities, BoundingBox rect)
        {
            // 收集矩形附近的线段/多段线作为箭头候选
            double margin = Math.Min(Math.Max(Math.Min(rect.Max.X - rect.Min.X, rect.Max.Y - rect.Min.Y) * 0.8, 5.0), 80.0);
            var zone = ExpandBox(rect, margin);
            var lines = entities.OfType<Line>().Where(l => Intersects(GetBoundingBox(l), zone)).ToList();
            var polylines = entities.OfType<LwPolyline>().Where(p => Intersects(GetBoundingBox(p), zone)).ToList();

            // 将多段线分解成线段
            foreach (var pl in polylines)
            {
                int cnt = pl.Vertices.Count;
                int segCount = pl.IsClosed ? cnt : cnt - 1;
                for (int i = 0; i < segCount; i++)
                {
                    var p1 = pl.Vertices[i].Location;
                    var p2 = pl.Vertices[(i + 1) % cnt].Location;
                    lines.Add(new Line(new XYZ(p1.X, p1.Y, 0), new XYZ(p2.X, p2.Y, 0)));
                }
            }

            if (lines.Count < 2) return new List<ArrowInfo>();

            // 端点聚类
            double tol = 2.0;
            var nodes = new List<XYZ>();
            int FindNode(XYZ p)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (Distance(nodes[i], p) <= tol) return i;
                }
                nodes.Add(p);
                return nodes.Count - 1;
            }

            var deg = new List<int>();
            var adj = new List<List<int>>();

            foreach (var l in lines)
            {
                int a = FindNode(l.StartPoint);
                int b = FindNode(l.EndPoint);
                while (deg.Count <= Math.Max(a, b))
                {
                    deg.Add(0);
                    adj.Add(new List<int>());
                }
                if (a == b) continue;
                deg[a]++; deg[b]++;
                adj[a].Add(b);
                adj[b].Add(a);
            }

            var arrows = new List<ArrowInfo>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (deg[i] < 2) continue;
                var neighbors = adj[i];
                for (int j = 0; j < neighbors.Count; j++)
                {
                    for (int k = j + 1; k < neighbors.Count; k++)
                    {
                        var v1 = Sub(nodes[neighbors[j]], nodes[i]);
                        var v2 = Sub(nodes[neighbors[k]], nodes[i]);
                        double ang = AngleBetween(v1, v2);
                        if (ang > 20 && ang < 140)
                        {
                            // 箭头尖为 nodes[i]，方向取两向量平均
                            var dir = new XYZ((v1.X + v2.X) * 0.5, (v1.Y + v2.Y) * 0.5, 0);
                            double len = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                            if (len < 1e-3) continue;
                            dir = new XYZ(dir.X / len, dir.Y / len, 0);
                            arrows.Add(new ArrowInfo { Tip = nodes[i], Direction = dir });
                        }
                    }
                }
            }

            return arrows;
        }

        private bool RatioWithin(double w, double h, double min, double max)
        {
            if (w <= 0 || h <= 0) return false;
            double ratio = Math.Max(w, h) / Math.Min(w, h);
            return ratio >= min && ratio <= max;
        }
        
        private bool HasManyLines(List<Entity> ents) => ents.OfType<Line>().Count() > 4;
        
        private XYZ Sub(XYZ a, XYZ b) => new XYZ(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        private double AngleBetween(XYZ a, XYZ b)
        {
            double dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            double na = Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
            double nb = Math.Sqrt(b.X * b.X + b.Y * b.Y + b.Z * b.Z);
            if (na < 1e-6 || nb < 1e-6) return 180.0;
            double cos = dot / (na * nb);
            cos = Math.Max(-1.0, Math.Min(1.0, cos));
            return Math.Acos(cos) * 180.0 / Math.PI;
        }
        
        private Direction CheckArrowDirection(List<Entity> entities, BoundingBox rect)
        {
            // 1) 取矩形内部的线段，构建端点图
            double margin = Math.Min(Math.Max(Math.Min(rect.Max.X - rect.Min.X, rect.Max.Y - rect.Min.Y) * 0.05, 1.0), 8.0);
            var innerLines = entities
                .OfType<Line>()
                .Where(l => IsInside(l.StartPoint, ExpandBox(rect, margin)) && IsInside(l.EndPoint, ExpandBox(rect, margin)))
                .ToList();
            if (innerLines.Count < 2) return Direction.Unknown;

            double tol = margin * 1.2;
            var points = new List<XYZ>();
            foreach (var l in innerLines)
            {
                points.Add(l.StartPoint);
                points.Add(l.EndPoint);
            }

            // 构建邻接：端点合并容差 tol
            var nodes = new List<XYZ>();
            foreach (var p in points)
            {
                bool found = false;
                foreach (var n in nodes)
                {
                    if (Distance(n, p) <= tol)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) nodes.Add(p);
            }

            int nNodes = nodes.Count;
            var deg = new int[nNodes];
            var adj = new List<int>[nNodes];
            for (int i = 0; i < nNodes; i++) adj[i] = new List<int>();

            int FindNode(XYZ p)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (Distance(nodes[i], p) <= tol) return i;
                }
                return -1;
            }

            foreach (var l in innerLines)
            {
                int a = FindNode(l.StartPoint);
                int b = FindNode(l.EndPoint);
                if (a < 0 || b < 0 || a == b) continue;
                adj[a].Add(b);
                adj[b].Add(a);
                deg[a]++; deg[b]++;
            }

            // 2) 找“箭头”候选：度数>=2 且夹角锐（20°~140°）
            int? headIdx = null;
            for (int i = 0; i < nNodes; i++)
            {
                if (deg[i] < 2) continue;
                var connected = adj[i];
                for (int j = 0; j < connected.Count; j++)
                {
                    for (int k = j + 1; k < connected.Count; k++)
                    {
                        var v1 = Sub(nodes[connected[j]], nodes[i]);
                        var v2 = Sub(nodes[connected[k]], nodes[i]);
                        double ang = AngleBetween(v1, v2);
                        if (ang > 20 && ang < 140)
                        {
                            headIdx = i;
                            break;
                        }
                    }
                    if (headIdx.HasValue) break;
                }
                if (headIdx.HasValue) break;
            }

            if (!headIdx.HasValue) return Direction.Unknown;
            var head = nodes[headIdx.Value];

            // 3) 根据矩形长轴确定方向，箭头尖端落在长轴哪一侧
            bool horizontal = (rect.Max.X - rect.Min.X) >= (rect.Max.Y - rect.Min.Y);
            double centerX = (rect.Max.X + rect.Min.X) * 0.5;
            double centerY = (rect.Max.Y + rect.Min.Y) * 0.5;
            double len = horizontal ? (rect.Max.X - rect.Min.X) : (rect.Max.Y - rect.Min.Y);
            if (len <= 0) return Direction.Unknown;

            double headProj = horizontal ? head.X : head.Y;
            double minProj = horizontal ? rect.Min.X : rect.Min.Y;
            double maxProj = horizontal ? rect.Max.X : rect.Max.Y;
            double centerProj = horizontal ? centerX : centerY;
            double nearTol = Math.Max(len * 0.1, 3.0);

            if (Math.Abs(headProj - maxProj) < nearTol) return Direction.Outward;
            if (Math.Abs(headProj - minProj) < nearTol) return Direction.Inward;

            // 如果箭头尖在中间区域，根据相对中心判断
            return headProj >= centerProj ? Direction.Outward : Direction.Inward;
        }
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

        public List<BoundingBox> GetEjectorValves(IEnumerable<Entity> allEntities)
        {
            var triangles = allEntities.OfType<LwPolyline>()
                .Where(pl => pl.IsClosed && pl.Vertices.Count == 3)
                .ToList();

            var result = new List<BoundingBox>();
            if (triangles.Count < 2) return result;

            var used = new HashSet<int>();
            for (int i = 0; i < triangles.Count; i++)
            {
                if (used.Contains(i)) continue;
                var b1 = GetBoundingBox(triangles[i]);
                double h1 = b1.Max.Y - b1.Min.Y;

                for (int j = i + 1; j < triangles.Count; j++)
                {
                    if (used.Contains(j)) continue;
                    var b2 = GetBoundingBox(triangles[j]);
                    double h2 = b2.Max.Y - b2.Min.Y;

                    // 1. 尺寸相近
                    if (Math.Abs(h1 - h2) > Math.Max(h1, h2) * 0.3) continue;

                    // 2. 距离极近 (通常小于三角形高度)
                    double gap = Math.Max(b1.Min.X - b2.Max.X, b2.Min.X - b1.Max.X);
                    if (gap > h1 * 1.5) continue;

                    // 3. Y 轴重叠/对齐
                    double yOverlap = Math.Min(b1.Max.Y, b2.Max.Y) - Math.Max(b1.Min.Y, b2.Min.Y);
                    if (yOverlap < h1 * 0.5) continue;

                    // 构成一个整体
                    used.Add(i);
                    used.Add(j);
                    
                    var combined = new BoundingBox(
                        new XYZ(Math.Min(b1.Min.X, b2.Min.X), Math.Min(b1.Min.Y, b2.Min.Y), 0),
                        new XYZ(Math.Max(b1.Max.X, b2.Max.X), Math.Max(b1.Max.Y, b2.Max.Y), 0));
                    result.Add(combined);
                    break;
                }
            }
            return result;
        }

        public BoundingBox PublicGetBoundingBox(Entity e)
        {
             return GetBoundingBox(e);
        }

        public BoundingBox PublicGetBoundingBox(List<Entity> entities)
        {
             return GetBoundingBox(entities);
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
