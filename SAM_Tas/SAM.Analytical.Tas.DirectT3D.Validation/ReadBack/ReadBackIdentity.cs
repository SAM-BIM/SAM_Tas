// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.InputEquivalence;
using EQ = SAM.Analytical.InputEquivalence.Query;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    public sealed class SurfaceMatch
    {
        public Guid PanelGuid;
        public int SurfaceIndex = -1;
        public IdentityStatus Status = IdentityStatus.NotEstablished;
        public bool SameFacing;
        public string Evidence;
    }

    public sealed class ApertureMatch
    {
        public Guid ApertureGuid;
        public Guid HostPanelGuid;
        public List<int> PaneSurfaces = new List<int>();
        public List<int> FrameSurfaces = new List<int>();
        public IdentityStatus Status = IdentityStatus.NotEstablished;
        public string Evidence;
    }

    public sealed class ZoneMatch
    {
        public Guid SpaceGuid;
        public int ZoneIndex = -1;
        public IdentityStatus Status = IdentityStatus.NotEstablished;
        public bool? CarrierAgrees;
        public string Evidence;
        public List<SurfaceMatch> Surfaces = new List<SurfaceMatch>();
        public List<ApertureMatch> Apertures = new List<ApertureMatch>();

        /// <summary>Native surfaces of the zone that no SAM panel or aperture claims.</summary>
        public List<int> UnmatchedSurfaces = new List<int>();
    }

    public sealed class IdentityResult
    {
        public List<ZoneMatch> Zones = new List<ZoneMatch>();

        /// <summary>Native zones no SAM space claims (an object SAM never stated).</summary>
        public List<int> UnmatchedZones = new List<int>();
        public List<string> Notes = new List<string>();

        public ZoneMatch For(Guid spaceGuid)
        {
            return Zones.Find(x => x.SpaceGuid == spaceGuid);
        }
    }

    /// <summary>
    /// One-to-one identity between the frozen SAM model and the persisted TBD from geometry alone (S08 plan R5), written
    /// fresh for the read-back - not <c>Query.Match</c>, not <c>UpdateIds</c> and not the writer's names, order or GUIDs.
    /// A space is a zone when the zone's opaque room-surface outlines and the space's panel outlines are the same set
    /// of boundaries (one to one, <see cref="EQ.MatchPolygons"/>); the writer's <c>[SpaceGuid]</c> carrier can only
    /// contradict such a match, never make one. Apertures are tied to the pane and frame surfaces that lie in their
    /// outline. Anything unmatched, many to one or contradicted is a mismatch.
    /// </summary>
    public static class ReadBackIdentity
    {
        public const string SpaceGuidMarker = "[SpaceGuid]=";

        // TBD BuildingElementType codes of the opening parts (GLAZING, ROOFLIGHT, DOORELEMENT, FRAMEELEMENT, VEHICLEDOOR).
        public static readonly int[] OpeningBETypes = { 12, 13, 14, 15, 20 };
        public const int FrameBEType = 15;

        public static bool IsOpening(NativeSurface surface)
        {
            return OpeningBETypes.Contains(surface.BEType);
        }

        public static IdentityResult Match(ExpectedModel expected, NativeSnapshot native, double tolerance = Core.Tolerance.MacroDistance)
        {
            IdentityResult result = new IdentityResult();
            List<NativeZone> zones = native.Zones ?? new List<NativeZone>();

            // Candidate pairs: every space against every zone, by the opaque boundary set.
            Dictionary<Guid, List<int>> candidates = new Dictionary<Guid, List<int>>();
            Dictionary<string, GeometryMatchResult> matches = new Dictionary<string, GeometryMatchResult>();
            foreach (ExpectedSpace space in expected.Spaces)
            {
                List<Polygon3D> panels = space.Panels.Select(x => Simplify(x.Outer, tolerance)).ToList();
                candidates[space.Guid] = new List<int>();
                for (int z = 0; z < zones.Count; z++)
                {
                    List<int> opaque = OpaqueSurfaces(zones[z]);
                    GeometryMatchResult match = EQ.MatchPolygons(panels, opaque.Select(i => NativeOuter(zones[z].Surfaces[i], tolerance)).ToList(), tolerance);
                    matches[space.Guid + "|" + z] = match;

                    // Every SAM panel pairs with exactly one native surface and no native surface with two panels. A native
                    // surface no panel claims does not hide the zone: it is reported below as an object SAM never stated.
                    if (match.UnmatchedExpected.Count == 0 && match.AmbiguousExpected.Count == 0 && match.AmbiguousNative.Count == 0 && match.Pairs.Count == panels.Count)
                    {
                        candidates[space.Guid].Add(z);
                    }
                }
            }

            Dictionary<int, int> zoneUse = new Dictionary<int, int>();
            foreach (List<int> list in candidates.Values)
            {
                foreach (int z in list)
                {
                    zoneUse.TryGetValue(z, out int count);
                    zoneUse[z] = count + 1;
                }
            }

            foreach (ExpectedSpace space in expected.Spaces)
            {
                ZoneMatch zoneMatch = new ZoneMatch { SpaceGuid = space.Guid };
                result.Zones.Add(zoneMatch);
                List<int> list = candidates[space.Guid];
                if (list.Count != 1 || zoneUse[list[0]] != 1)
                {
                    zoneMatch.Status = IdentityStatus.Mismatch;
                    zoneMatch.Evidence = list.Count == 0 ? "no native zone has this space's panel boundaries" : "ambiguous: " + list.Count + " candidate zone(s), or a zone claimed by more than one space";
                    continue;
                }

                int zoneIndex = list[0];
                NativeZone zone = zones[zoneIndex];
                zoneMatch.ZoneIndex = zoneIndex;
                zoneMatch.CarrierAgrees = Carrier(zone.Description, out Guid carrier) ? carrier == space.Guid : (bool?)null;

                GeometryMatchResult geometry = matches[space.Guid + "|" + zoneIndex];
                zoneMatch.Status = EQ.AssessIdentity(geometry, 0, zoneMatch.CarrierAgrees);
                zoneMatch.Evidence = "panel boundaries match the zone's opaque room surfaces one to one (" + geometry.Pairs.Count + " pairs)" +
                                     (zoneMatch.CarrierAgrees == null ? "; no [SpaceGuid] carrier" : zoneMatch.CarrierAgrees == true ? "; the [SpaceGuid] carrier agrees" : "; the [SpaceGuid] carrier CONTRADICTS the geometry");

                List<int> opaque = OpaqueSurfaces(zone);
                HashSet<int> claimed = new HashSet<int>();
                for (int p = 0; p < space.Panels.Count; p++)
                {
                    GeometryPair pair = geometry.PairForExpected(p);
                    SurfaceMatch surfaceMatch = new SurfaceMatch { PanelGuid = space.Panels[p].Guid };
                    if (pair == null || zoneMatch.Status == IdentityStatus.Mismatch)
                    {
                        surfaceMatch.Status = IdentityStatus.Mismatch;
                        surfaceMatch.Evidence = "no one-to-one boundary match";
                    }
                    else
                    {
                        surfaceMatch.SurfaceIndex = opaque[pair.NativeIndex];
                        surfaceMatch.SameFacing = pair.SameFacing;
                        surfaceMatch.Status = zoneMatch.Status == IdentityStatus.Verified ? IdentityStatus.Verified : zoneMatch.Status;
                        surfaceMatch.Evidence = "boundary match; native winding " + (pair.SameFacing ? "same as" : "opposite to") + " the panel";
                        claimed.Add(surfaceMatch.SurfaceIndex);
                    }

                    zoneMatch.Surfaces.Add(surfaceMatch);
                }

                MatchApertures(space, zone, zoneMatch, claimed, tolerance);

                for (int i = 0; i < zone.Surfaces.Count; i++)
                {
                    if (!claimed.Contains(i))
                    {
                        zoneMatch.UnmatchedSurfaces.Add(i);
                    }
                }
            }

            for (int z = 0; z < zones.Count; z++)
            {
                if (!result.Zones.Any(x => x.ZoneIndex == z && x.Status != IdentityStatus.Mismatch))
                {
                    result.UnmatchedZones.Add(z);
                }
            }

            return result;
        }

        private static void MatchApertures(ExpectedSpace space, NativeZone zone, ZoneMatch zoneMatch, HashSet<int> claimed, double tolerance)
        {
            List<KeyValuePair<ExpectedPanel, ExpectedAperture>> apertures = space.Panels.SelectMany(p => p.Apertures.Select(a => new KeyValuePair<ExpectedPanel, ExpectedAperture>(p, a))).ToList();
            Dictionary<Guid, ApertureMatch> byGuid = new Dictionary<Guid, ApertureMatch>();
            foreach (KeyValuePair<ExpectedPanel, ExpectedAperture> item in apertures)
            {
                ApertureMatch match = new ApertureMatch { ApertureGuid = item.Value.Guid, HostPanelGuid = item.Key.Guid };
                byGuid[item.Value.Guid] = match;
                zoneMatch.Apertures.Add(match);
            }

            List<string> problems = new List<string>();
            for (int i = 0; i < zone.Surfaces.Count; i++)
            {
                NativeSurface surface = zone.Surfaces[i];
                if (!IsOpening(surface))
                {
                    continue;
                }

                List<double[]> outer = surface.RoomSurfaces.Count == 1 ? surface.RoomSurfaces[0].Outer : null;
                List<Guid> owners = outer == null ? new List<Guid>() : apertures.Where(x => Within(outer, x.Value.Outer, tolerance)).Select(x => x.Value.Guid).ToList();
                if (owners.Count != 1)
                {
                    problems.Add("opening surface " + i + " lies in " + owners.Count + " aperture outline(s)");
                    continue;
                }

                ApertureMatch match = byGuid[owners[0]];
                (surface.BEType == FrameBEType ? match.FrameSurfaces : match.PaneSurfaces).Add(i);
                claimed.Add(i);
            }

            foreach (ApertureMatch match in zoneMatch.Apertures)
            {
                if (zoneMatch.Status == IdentityStatus.Mismatch)
                {
                    match.Status = IdentityStatus.Mismatch;
                    match.Evidence = "the zone itself is not identified";
                }
                else if (match.PaneSurfaces.Count == 0)
                {
                    match.Status = IdentityStatus.Mismatch;
                    match.Evidence = "no native pane surface lies in the aperture outline";
                }
                else
                {
                    match.Status = zoneMatch.Status;
                    match.Evidence = match.PaneSurfaces.Count + " pane and " + match.FrameSurfaces.Count + " frame surface(s) lie in the aperture outline";
                }
            }

            foreach (string problem in problems)
            {
                zoneMatch.Evidence += "; " + problem;
            }
        }

        // ---- geometry helpers -------------------------------------------------------------------------------------------

        public static List<int> OpaqueSurfaces(NativeZone zone)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < zone.Surfaces.Count; i++)
            {
                if (!IsOpening(zone.Surfaces[i]))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        /// <summary>The native outline of a zone surface, or null when it does not have exactly one readable room surface.</summary>
        public static Polygon3D NativeOuter(NativeSurface surface, double tolerance)
        {
            if (surface?.RoomSurfaces == null || surface.RoomSurfaces.Count != 1 || surface.RoomSurfaces[0].Outer == null || surface.RoomSurfaces[0].Outer.Count < 3)
            {
                return null;
            }

            return Simplify(new Polygon3D(surface.RoomSurfaces[0].Outer.Select(x => new Point3D(x[0], x[1], x[2])).ToList()), tolerance);
        }

        /// <summary>Drops repeated and collinear vertices (both sides alike), so the same boundary compares equal however it is sampled.</summary>
        public static Polygon3D Simplify(Polygon3D polygon, double tolerance)
        {
            List<Point3D> points = polygon?.GetPoints();
            if (points == null || points.Count < 3)
            {
                return polygon;
            }

            bool changed = true;
            while (changed && points.Count > 3)
            {
                changed = false;
                for (int i = 0; i < points.Count; i++)
                {
                    Point3D a = points[(i + points.Count - 1) % points.Count], b = points[i], c = points[(i + 1) % points.Count];
                    if (a.Distance(b) <= tolerance || DistanceToLine(b, a, c) <= tolerance)
                    {
                        points.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }

            return new Polygon3D(points);
        }

        private static double DistanceToLine(Point3D p, Point3D a, Point3D c)
        {
            double ux = c.X - a.X, uy = c.Y - a.Y, uz = c.Z - a.Z;
            double length = System.Math.Sqrt(ux * ux + uy * uy + uz * uz);
            if (length < 1e-12)
            {
                return p.Distance(a);
            }

            double vx = p.X - a.X, vy = p.Y - a.Y, vz = p.Z - a.Z;
            double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
            return System.Math.Sqrt(cx * cx + cy * cy + cz * cz) / length;
        }

        /// <summary>Every point of <paramref name="points"/> lies in the plane of <paramref name="outline"/> and inside it (boundary included).</summary>
        public static bool Within(List<double[]> points, Polygon3D outline, double tolerance)
        {
            List<Point3D> outlinePoints = outline?.GetPoints();
            if (points == null || outlinePoints == null || outlinePoints.Count < 3)
            {
                return false;
            }

            Plane plane = outline.GetPlane();
            List<Geometry.Planar.Point2D> polygon = outlinePoints.ConvertAll(x => plane.Convert(x));
            foreach (double[] xyz in points)
            {
                Point3D point = new Point3D(xyz[0], xyz[1], xyz[2]);
                if (plane.Distance(point) > tolerance)
                {
                    return false;
                }

                Geometry.Planar.Point2D q = plane.Convert(point);
                if (!Inside(polygon, q) && BoundaryDistance(polygon, q) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Inside(List<Geometry.Planar.Point2D> polygon, Geometry.Planar.Point2D q)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((polygon[i].Y > q.Y) != (polygon[j].Y > q.Y) && q.X < (polygon[j].X - polygon[i].X) * (q.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static double BoundaryDistance(List<Geometry.Planar.Point2D> polygon, Geometry.Planar.Point2D q)
        {
            double best = double.MaxValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                Geometry.Planar.Point2D a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                double t = l2 < 1e-24 ? 0 : System.Math.Max(0, System.Math.Min(1, ((q.X - a.X) * dx + (q.Y - a.Y) * dy) / l2));
                double ex = a.X + t * dx - q.X, ey = a.Y + t * dy - q.Y;
                best = System.Math.Min(best, System.Math.Sqrt(ex * ex + ey * ey));
            }

            return best;
        }

        public static bool Carrier(string description, out Guid spaceGuid)
        {
            spaceGuid = Guid.Empty;
            if (string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            foreach (string segment in description.Split(';'))
            {
                string text = segment.Trim();
                if (text.StartsWith(SpaceGuidMarker, StringComparison.Ordinal))
                {
                    return Guid.TryParse(text.Substring(SpaceGuidMarker.Length).Trim(), out spaceGuid);
                }
            }

            return false;
        }
    }
}
