// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>One expected polygon paired with one native polygon by geometry alone.</summary>
    public sealed class GeometryPair
    {
        public GeometryPair(int expectedIndex, int nativeIndex, bool sameFacing)
        {
            ExpectedIndex = expectedIndex;
            NativeIndex = nativeIndex;
            SameFacing = sameFacing;
        }

        public int ExpectedIndex { get; }
        public int NativeIndex { get; }

        /// <summary>True when the two polygons wind the same way. Reported only: the native winding convention is an open native question (N-8).</summary>
        public bool SameFacing { get; }
    }

    /// <summary>The result of matching authored polygons to native polygons by vertex geometry, with no name, order or GUID.</summary>
    public sealed class GeometryMatchResult
    {
        public GeometryMatchResult(IReadOnlyList<GeometryPair> pairs, IReadOnlyList<int> unmatchedExpected, IReadOnlyList<int> unmatchedNative, IReadOnlyList<int> ambiguousExpected, IReadOnlyList<int> ambiguousNative)
        {
            Pairs = pairs;
            UnmatchedExpected = unmatchedExpected;
            UnmatchedNative = unmatchedNative;
            AmbiguousExpected = ambiguousExpected;
            AmbiguousNative = ambiguousNative;
        }

        public IReadOnlyList<GeometryPair> Pairs { get; }

        /// <summary>Expected polygons with no native polygon (EXPORT-DEFECT).</summary>
        public IReadOnlyList<int> UnmatchedExpected { get; }

        /// <summary>Native polygons with no expected polygon (EXPORT-DEFECT: an object SAM never stated).</summary>
        public IReadOnlyList<int> UnmatchedNative { get; }

        /// <summary>Expected polygons that fit more than one native polygon, or that share a native polygon with another expected one (many to one: identity FAIL).</summary>
        public IReadOnlyList<int> AmbiguousExpected { get; }

        public IReadOnlyList<int> AmbiguousNative { get; }

        /// <summary>A one-to-one match of every expected and every native polygon.</summary>
        public bool IsBijection => UnmatchedExpected.Count == 0 && UnmatchedNative.Count == 0 && AmbiguousExpected.Count == 0 && AmbiguousNative.Count == 0;

        public GeometryPair PairForExpected(int expectedIndex)
        {
            return Pairs.FirstOrDefault(x => x.ExpectedIndex == expectedIndex);
        }
    }

    public static partial class Query
    {
        /// <summary>
        /// Matches authored polygons to native polygons by vertex geometry only: two polygons match when they have the
        /// same number of vertices and every vertex of one lies within <paramref name="tolerance"/> of a distinct vertex of
        /// the other, whatever the start vertex and winding. Names, array order and GUIDs are not inputs.
        /// </summary>
        public static GeometryMatchResult MatchPolygons(IReadOnlyList<Polygon3D> expected, IReadOnlyList<Polygon3D> native, double tolerance = Core.Tolerance.MacroDistance)
        {
            List<List<Point3D>> e = (expected ?? new List<Polygon3D>()).Select(x => x?.GetPoints()).ToList();
            List<List<Point3D>> n = (native ?? new List<Polygon3D>()).Select(x => x?.GetPoints()).ToList();

            List<int>[] candidates = new List<int>[e.Count];
            int[] nativeUse = new int[n.Count];
            for (int i = 0; i < e.Count; i++)
            {
                candidates[i] = new List<int>();
                if (e[i] == null)
                {
                    continue;
                }

                for (int j = 0; j < n.Count; j++)
                {
                    if (n[j] != null && SameVertices(e[i], n[j], tolerance))
                    {
                        candidates[i].Add(j);
                        nativeUse[j]++;
                    }
                }
            }

            List<GeometryPair> pairs = new List<GeometryPair>();
            List<int> unmatchedExpected = new List<int>();
            List<int> ambiguousExpected = new List<int>();
            for (int i = 0; i < e.Count; i++)
            {
                if (candidates[i].Count == 0)
                {
                    unmatchedExpected.Add(i);
                }
                else if (candidates[i].Count > 1 || nativeUse[candidates[i][0]] > 1)
                {
                    ambiguousExpected.Add(i);
                }
                else
                {
                    pairs.Add(new GeometryPair(i, candidates[i][0], NewellNormal(e[i]).DotWith(NewellNormal(n[candidates[i][0]])) > 0));
                }
            }

            List<int> unmatchedNative = new List<int>();
            List<int> ambiguousNative = new List<int>();
            for (int j = 0; j < n.Count; j++)
            {
                if (nativeUse[j] == 0)
                {
                    unmatchedNative.Add(j);
                }
                else if (nativeUse[j] > 1 || candidates.Count(c => c.Contains(j) && c.Count > 1) > 0)
                {
                    ambiguousNative.Add(j);
                }
            }

            return new GeometryMatchResult(pairs, unmatchedExpected, unmatchedNative, ambiguousExpected, ambiguousNative);
        }

        /// <summary>
        /// The identity of one expected polygon, from independent geometry. A one-to-one geometric match is
        /// <see cref="IdentityStatus.Verified"/> unless a writer carrier (for example the zone description GUID)
        /// contradicts it; a carrier agreeing without geometry is only <see cref="IdentityStatus.WriterOnly"/>; no
        /// evidence is <see cref="IdentityStatus.NotEstablished"/>; an unmatched or ambiguous polygon is a mismatch.
        /// </summary>
        /// <param name="geometry">The geometric match, or null when no native geometry was read.</param>
        /// <param name="expectedIndex">The expected polygon being assessed.</param>
        /// <param name="writerCarrierAgrees">What the writer-minted carrier (name, order, GUID) says about the pairing, or null when it has no opinion.</param>
        public static IdentityStatus AssessIdentity(GeometryMatchResult geometry, int expectedIndex, bool? writerCarrierAgrees)
        {
            if (writerCarrierAgrees == false)
            {
                return IdentityStatus.Mismatch;
            }

            if (geometry == null)
            {
                return writerCarrierAgrees == true ? IdentityStatus.WriterOnly : IdentityStatus.NotEstablished;
            }

            return geometry.PairForExpected(expectedIndex) != null ? IdentityStatus.Verified : IdentityStatus.Mismatch;
        }

        private static bool SameVertices(List<Point3D> a, List<Point3D> b, double tolerance)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            bool[] used = new bool[b.Count];
            foreach (Point3D point in a)
            {
                int found = -1;
                double best = double.MaxValue;
                for (int j = 0; j < b.Count; j++)
                {
                    if (used[j])
                    {
                        continue;
                    }

                    double distance = point.Distance(b[j]);
                    if (distance <= tolerance && distance < best)
                    {
                        best = distance;
                        found = j;
                    }
                }

                if (found < 0)
                {
                    return false;
                }

                used[found] = true;
            }

            return true;
        }

        private struct Vec
        {
            public double X, Y, Z;
            public double DotWith(Vec o) { return X * o.X + Y * o.Y + Z * o.Z; }
        }

        private static Vec NewellNormal(List<Point3D> points)
        {
            Vec result = new Vec();
            for (int i = 0; i < points.Count; i++)
            {
                Point3D c = points[i];
                Point3D n = points[(i + 1) % points.Count];
                result.X += (c.Y - n.Y) * (c.Z + n.Z);
                result.Y += (c.Z - n.Z) * (c.X + n.X);
                result.Z += (c.X - n.X) * (c.Y + n.Y);
            }

            return result;
        }
    }
}
