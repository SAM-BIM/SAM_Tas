// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    /// <summary>
    /// The one place a SAM polygon becomes the coordinate array <c>WrImportIDF</c> takes. Every surface, opening
    /// and shade of the direct SAM -> T3D route goes through these methods so that they all agree on what a
    /// valid polygon is, and none of them carries its own slightly different cleaning.
    /// <para>
    /// <b>What TAS requires</b> (measured on Tas 9.5.7, see <c>Convert.ToT3D</c>): a <c>double[3, n]</c> whose
    /// row 0 is X, row 1 is Y and row 2 is Z in metres; an OPEN loop (no repeated first vertex); and vertices
    /// that run counter-clockwise as seen from outside, so the polygon normal points out of the zone. A
    /// <c>double[n, 3]</c> is accepted and silently wrong (half the area), and a flat <c>double[]</c> crashes the
    /// TAS COM server.
    /// </para>
    /// </summary>
    public static partial class Query
    {
        /// <summary>
        /// A SAM loop as a clean, open, non-degenerate polygon: the closing vertex that repeats the first is
        /// dropped, vertices within <paramref name="tolerance"/> of the previous one are merged, and vertices
        /// that lie within <paramref name="tolerance"/> of the line through their neighbours (collinear points
        /// and zero-width spikes) are removed. The order of the remaining vertices - and so the orientation - is
        /// the input's.
        /// </summary>
        /// <param name="point3Ds">The loop, closed or open.</param>
        /// <param name="tolerance">Distance [m] for "same point" and "on the line".</param>
        /// <returns>The polygon, or null when fewer than three vertices or no area remain.</returns>
        public static List<Point3D> TasPolygon(this IEnumerable<Point3D> point3Ds, double tolerance = Core.Tolerance.MacroDistance)
        {
            if (point3Ds == null)
            {
                return null;
            }

            List<Point3D> result = new List<Point3D>();
            foreach (Point3D point3D in point3Ds)
            {
                if (point3D == null || double.IsNaN(point3D.X) || double.IsNaN(point3D.Y) || double.IsNaN(point3D.Z))
                {
                    continue;
                }

                if (result.Count != 0 && result[result.Count - 1].Distance(point3D) <= tolerance)
                {
                    continue;
                }

                result.Add(point3D);
            }

            //The closing vertex, and any run of vertices that wrapped back onto the first one.
            while (result.Count > 1 && result[0].Distance(result[result.Count - 1]) <= tolerance)
            {
                result.RemoveAt(result.Count - 1);
            }

            //Collinear vertices. Removing one can make its neighbour collinear in turn, so repeat to a fixed
            //point. Each pass removes at most one vertex per index it visits, so this ends.
            bool removed = true;
            while (removed && result.Count >= 3)
            {
                removed = false;
                for (int i = 0; i < result.Count && result.Count >= 3; i++)
                {
                    Point3D previous = result[(i + result.Count - 1) % result.Count];
                    Point3D current = result[i];
                    Point3D next = result[(i + 1) % result.Count];

                    if (DistanceToLine(current, previous, next) <= tolerance)
                    {
                        result.RemoveAt(i);
                        removed = true;
                        i--;
                    }
                }
            }

            if (result.Count < 3)
            {
                return null;
            }

            //A polygon whose vertices are not collinear but whose enclosed area is nil at this tolerance
            //(e.g. a loop that doubles back on itself) is not a surface.
            Vector3D normal = NewellNormal(result);
            if (normal == null || normal.Length <= tolerance * tolerance)
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// <see cref="TasPolygon(IEnumerable{Point3D}, double)"/>, then oriented so its normal (right-hand rule
        /// over the vertex order) points the way <paramref name="normal"/> does - for a surface, out of its zone.
        /// </summary>
        /// <param name="point3Ds">The loop.</param>
        /// <param name="normal">The required normal direction; null leaves the order as it is.</param>
        /// <param name="tolerance">Distance [m] for the cleaning.</param>
        /// <param name="reversed">True when the vertex order was reversed to match <paramref name="normal"/>.</param>
        /// <returns>The polygon, or null when the loop is degenerate.</returns>
        public static List<Point3D> TasPolygon(this IEnumerable<Point3D> point3Ds, Vector3D normal, double tolerance, out bool reversed)
        {
            reversed = false;

            List<Point3D> result = TasPolygon(point3Ds, tolerance);
            if (result == null || normal == null)
            {
                return result;
            }

            Vector3D normal_Polygon = NewellNormal(result);
            if (normal_Polygon != null && Dot(normal_Polygon, normal) < 0)
            {
                result.Reverse();
                reversed = true;
            }

            return result;
        }

        /// <summary>
        /// The outer loop of a <see cref="Face3D"/> as a TAS polygon, oriented to <paramref name="normal"/>.
        /// Internal edges (holes) are not part of the result: <c>AddSurface</c> takes one outer loop, so the
        /// caller reports <paramref name="holes"/> rather than letting them vanish unremarked.
        /// </summary>
        public static List<Point3D> TasPolygon(this Face3D face3D, Vector3D normal, double tolerance, out bool reversed, out int holes)
        {
            reversed = false;
            holes = 0;

            if (face3D == null)
            {
                return null;
            }

            holes = face3D.GetInternalEdge3Ds()?.Count ?? 0;

            ISegmentable3D segmentable3D = face3D.GetExternalEdge3D() as ISegmentable3D;
            if (segmentable3D == null)
            {
                return null;
            }

            return TasPolygon(segmentable3D.GetPoints(), normal, tolerance, out reversed);
        }

        /// <summary>
        /// The polygon as the array <c>WrImportIDF</c> takes: <c>double[3, n]</c>, X / Y / Z in rows 0 / 1 / 2.
        /// The layout is not a free choice - see the remarks on <see cref="Query"/>.
        /// </summary>
        public static double[,] ToTasCoordinates(this IList<Point3D> point3Ds)
        {
            if (point3Ds == null || point3Ds.Count == 0)
            {
                return null;
            }

            double[,] result = new double[3, point3Ds.Count];
            for (int i = 0; i < point3Ds.Count; i++)
            {
                result[0, i] = point3Ds[i].X;
                result[1, i] = point3Ds[i].Y;
                result[2, i] = point3Ds[i].Z;
            }

            return result;
        }

        /// <summary>
        /// The (unnormalised) Newell normal of an ordered loop: its direction is the polygon normal by the
        /// right-hand rule over the vertex order and its length is twice the polygon area. Null for fewer than
        /// three vertices.
        /// </summary>
        public static Vector3D NewellNormal(this IList<Point3D> point3Ds)
        {
            if (point3Ds == null || point3Ds.Count < 3)
            {
                return null;
            }

            double x = 0;
            double y = 0;
            double z = 0;
            for (int i = 0; i < point3Ds.Count; i++)
            {
                Point3D current = point3Ds[i];
                Point3D next = point3Ds[(i + 1) % point3Ds.Count];

                x += (current.Y - next.Y) * (current.Z + next.Z);
                y += (current.Z - next.Z) * (current.X + next.X);
                z += (current.X - next.X) * (current.Y + next.Y);
            }

            return new Vector3D(x, y, z);
        }

        /// <summary>
        /// Moves each vertex onto the plane through <paramref name="origin"/> with normal
        /// <paramref name="normal"/> (straight along the normal), and reports how far the worst vertex moved.
        /// </summary>
        public static List<Point3D> ProjectedOnPlane(this IEnumerable<Point3D> point3Ds, Point3D origin, Vector3D normal, out double maxDistance)
        {
            maxDistance = 0;

            if (point3Ds == null || origin == null || normal == null || normal.Length == 0)
            {
                return null;
            }

            Vector3D unit = normal.Unit;
            List<Point3D> result = new List<Point3D>();
            foreach (Point3D point3D in point3Ds)
            {
                if (point3D == null)
                {
                    continue;
                }

                double distance = Dot(new Vector3D(origin, point3D), unit);
                maxDistance = global::System.Math.Max(maxDistance, global::System.Math.Abs(distance));

                result.Add(new Point3D(point3D.X - unit.X * distance, point3D.Y - unit.Y * distance, point3D.Z - unit.Z * distance));
            }

            return result;
        }

        private static double Dot(Vector3D vector3D_1, Vector3D vector3D_2)
        {
            return vector3D_1.X * vector3D_2.X + vector3D_1.Y * vector3D_2.Y + vector3D_1.Z * vector3D_2.Z;
        }

        //Perpendicular distance from a point to the INFINITE line through two points; the distance to the first
        //when the two coincide.
        private static double DistanceToLine(Point3D point3D, Point3D lineStart, Point3D lineEnd)
        {
            Vector3D line = new Vector3D(lineStart, lineEnd);
            double length = line.Length;
            if (length <= double.Epsilon)
            {
                return point3D.Distance(lineStart);
            }

            Vector3D toPoint = new Vector3D(lineStart, point3D);
            Vector3D cross = new Vector3D(
                line.Y * toPoint.Z - line.Z * toPoint.Y,
                line.Z * toPoint.X - line.X * toPoint.Z,
                line.X * toPoint.Y - line.Y * toPoint.X);

            return cross.Length / length;
        }
    }
}
