// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static Space Match(this TAS3D.Zone zone, IEnumerable<Space> spaces)
        {
            if (spaces == null || zone == null)
                return null;

            foreach (Space space in spaces)
            {
                if (zone.name.Equals(space.Name))
                    return space;
            }

            return null;
        }

        public static Space Match(this Core.Tas.UKBR.Zone zone, IEnumerable<Space> spaces)
        {
            if(zone == null || spaces == null)
            {
                return null;
            }

            foreach(Space space in spaces)
            {
                if(space == null)
                {
                    continue;
                }

                if(!space.TryGetValue(SpaceParameter.ZoneGuid, out string zoneGuid) || string.IsNullOrWhiteSpace(zoneGuid))
                {
                    continue;
                }

                if(!Guid.TryParse(zoneGuid, out Guid guid))
                {
                    continue;
                }

                if(zone.GUID == guid)
                {
                    return space;
                }
            }

            foreach (Space space in spaces)
            {
                if(space?.Name == zone.Name)
                {
                    return space;
                }
            }

            return null;
        }

        public static Space Match(this IEnumerable<Space> spaces, string name, bool caseSensitive = true, bool trim = false)
        {
            if (spaces == null || !spaces.Any())
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string name_Temp = name;

            if (trim)
            {
                name_Temp = name_Temp.Trim();
            }

            if (!caseSensitive)
            {
                name_Temp = name_Temp.ToUpper();
            }

            foreach (Space space in spaces)
            {
                string name_Space = space?.Name;
                if (string.IsNullOrWhiteSpace(name_Space))
                {
                    continue;
                }

                if (trim)
                {
                    name_Space = name_Space.Trim();
                }

                if (!caseSensitive)
                {
                    name_Space = name_Space.ToUpper();
                }

                if (name_Space.Equals(name_Temp))
                {
                    return space;
                }
            }

            return null;
        }

        public static TBD.zone Match(this IEnumerable<TBD.zone> zones, string name, bool caseSensitive = true, bool trim = false)
        {
            if (zones == null || !zones.Any())
                return null;

            if (string.IsNullOrWhiteSpace(name))
                return null;

            string name_Temp = name;

            if (trim)
                name_Temp = name_Temp.Trim();

            if (!caseSensitive)
                name_Temp = name_Temp.ToUpper();

            foreach (TBD.zone zone in zones)
            {
                string name_Zone = zone?.name;
                if (string.IsNullOrWhiteSpace(name_Zone))
                    continue;

                if (trim)
                    name_Zone = name_Zone.Trim();

                if (!caseSensitive)
                    name_Zone = name_Zone.ToUpper();

                if (name_Zone.Equals(name_Temp))
                    return zone;
            }

            return null;
        }

        public static TBD.zone Match(this Space space, IEnumerable<TBD.zone> zones)
        {
            if (space == null || zones == null)
            {
                return null;
            }

            TBD.zone result = null;
            if (space.TryGetValue(SpaceParameter.ZoneGuid, out string zoneGuid) && !string.IsNullOrWhiteSpace(zoneGuid))
            {
                result = zones.ToList().Find(x => x.GUID == zoneGuid);
            }

            if (result == null)
            {
                result = zones.ToList().Find(x => x.name == space.Name);
            }

            return result;
        }

        public static Panel Match(this TBD.IZoneSurface zoneSurface, List<Panel> panels, double tolerance = Core.Tolerance.MacroDistance)
        {
            return Match(zoneSurface, panels, null, tolerance);
        }

        /// <summary>
        /// The binary-compatible zone-aware panel match. Existing compiled callers bind to this exact CLR
        /// signature; translation-aware workflow code uses the overload that also accepts a vector.
        /// </summary>
        public static Panel Match(this TBD.IZoneSurface zoneSurface, List<Panel> panels, string zoneGuid, double tolerance = Core.Tolerance.MacroDistance)
        {
            return Match(zoneSurface, panels, zoneGuid, tolerance, null);
        }

        /// <summary>
        /// The panel one zone surface belongs to, resolved from the stamps first and from geometry second.
        /// <para>
        /// <b><paramref name="zoneGuid"/> is the half of physical identity a surface number does not carry.</b>
        /// TAS numbers surfaces PER ZONE, so zone A's surface 5 and zone B's surface 5 are different surfaces
        /// that share a number, and the stamp comparison this overload exists for used to test the number
        /// alone - matching the first same-numbered panel in whichever zone was scanned first. Passing the
        /// surface's own zone makes the comparison identify a surface instead of a number. Omitting it (the
        /// parameterless overload) keeps the old number-only behaviour, which is what a caller with no zone in
        /// hand had before; the comparison itself also falls back to the number whenever either side states no
        /// zone, so this is a strict tightening - nothing that matched stops matching except a same-numbered
        /// surface in a different, GUID-stated zone.
        /// </para>
        /// <para>
        /// <b><paramref name="translation"/> compensates the gbXML route's coordinate shift.</b> TAS's own
        /// gbXML import recentres the building footprint on the origin, so on that route a TBD room surface's
        /// geometry sits at SAM-coordinates-minus-footprint-centre. The stamp comparison above is
        /// coordinate-free and unaffected; the geometric fallback below moves the surface's internal point by
        /// this vector before testing it against the (SAM-coordinate) panels. Null means no compensation -
        /// exactly the previous behaviour.
        /// </para>
        /// </summary>
        public static Panel Match(this TBD.IZoneSurface zoneSurface, List<Panel> panels, string zoneGuid, double tolerance, Vector3D translation)
        {
            if (zoneSurface == null || panels == null  || panels.Count == 0)
            {
                return null;
            }

            Core.Tas.ZoneSurfaceReference zoneSurfaceReference_Surface = new Core.Tas.ZoneSurfaceReference(zoneSurface.number, zoneGuid);

            foreach (Panel panel in panels)
            {
                if (panel == null)
                {
                    continue;
                }

                if (panel.TryGetValue(PanelParameter.ZoneSurfaceReference_1, out Core.Tas.ZoneSurfaceReference zoneSurfaceReference_1) && zoneSurfaceReference_1 != null)
                {
                    if (ZoneSurfaceReferencesMatch(zoneSurfaceReference_1, zoneSurfaceReference_Surface))
                    {
                        return panel;
                    }
                }

                //Tested against the _2 stamp. This read _1 again - a copy/paste that made the second slot
                //unreachable, and threw outright on a panel carrying _2 without _1.
                if (panel.TryGetValue(PanelParameter.ZoneSurfaceReference_2, out Core.Tas.ZoneSurfaceReference zoneSurfaceReference_2) && zoneSurfaceReference_2 != null)
                {
                    if (ZoneSurfaceReferencesMatch(zoneSurfaceReference_2, zoneSurfaceReference_Surface))
                    {
                        return panel;
                    }
                }
            }

            List<TBD.IRoomSurface> roomSurfaces = zoneSurface.RoomSurfaces();
            if(roomSurfaces == null || roomSurfaces.Count == 0)
            {
                return null;
            }

            foreach(TBD.IRoomSurface roomSurface in roomSurfaces)
            {
                Polygon3D polygon3D = Geometry.Tas.Convert.ToSAM(roomSurface?.GetPerimeter()?.GetFace());
                if (polygon3D == null)
                {
                    continue;
                }

                Point3D point3D = polygon3D.InternalPoint3D();
                if(point3D == null)
                {
                    continue;
                }

                //The point is in TBD coordinates; the panels are in SAM coordinates. On the gbXML route TAS
                //recentres the building footprint on the origin, so the two differ by a constant translation
                //which the caller (Modify.UpdateIds) has computed - move the point, never the panels.
                if (translation != null)
                {
                    point3D = point3D.GetMoved(translation) as Point3D;
                    if (point3D == null)
                    {
                        continue;
                    }
                }

                foreach(Panel panel in panels)
                {
                    BoundingBox3D boundingBox3D = panel?.GetBoundingBox();
                    if(boundingBox3D == null)
                    {
                        continue;
                    }

                    // Bounding box vs. room-surface internal point — guards the expensive Face3D.InRange
                    // point-in-face test on the next line. Previously compared the panel bbox to itself,
                    // which is a tautology, defeating the prefilter.
                    if (!boundingBox3D.InRange(point3D, tolerance))
                    {
                        continue;
                    }

                    Face3D face3D = panel.GetFace3D(false);
                    if(face3D == null)
                    {
                        continue;
                    }

                    if (InRangeOutsideHoles(face3D, point3D, tolerance))
                    {
                        return panel;
                    }
                }
            }

            //Building-element widths. With TAS's UseBEWidths ON a surface is not where its SAM panel is: TAS
            //treats the polygon as the element's centre line and moves each zone's surface half the element
            //width into the zone (measured on the house model: an external wall 0.25 m wide lands 0.125 m in
            //from the panel). Nothing within tolerance of the panel then, so - only once the exact test above
            //has found nothing - a surface parallel to a panel, at most half its own element's width (plus
            //tolerance) away from it and inside it once projected onto it, belongs to it. Of several, the
            //nearest. With widths OFF the surfaces are on the panels and the exact test decides, as before.
            //A zero-width element (Air) has no centre line of its own: TAS moves it with the zone's offset surfaces round it
            //(house model: the stair-void Air surface sits with the 0.20 m slab's half width). Its bound is then the widest
            //half width in its zone.
            double halfWidth = zoneSurface.buildingElement == null ? 0 : zoneSurface.buildingElement.width / 2.0;
            if (halfWidth <= tolerance)
            {
                List<TBD.IZoneSurface> zoneSurfaces_Zone = zoneSurface.zone?.ZoneSurfaces();
                if (zoneSurfaces_Zone != null)
                {
                    foreach (TBD.IZoneSurface zoneSurface_Zone in zoneSurfaces_Zone)
                    {
                        TBD.buildingElement buildingElement_Zone = zoneSurface_Zone?.buildingElement;
                        if (buildingElement_Zone != null && buildingElement_Zone.width / 2.0 > halfWidth)
                        {
                            halfWidth = buildingElement_Zone.width / 2.0;
                        }
                    }
                }
            }

            if (halfWidth <= tolerance)
            {
                return null;
            }

            Panel result = null;
            double distance_Min = double.MaxValue;
            foreach (TBD.IRoomSurface roomSurface in roomSurfaces)
            {
                Polygon3D polygon3D = Geometry.Tas.Convert.ToSAM(roomSurface?.GetPerimeter()?.GetFace());
                Point3D point3D = polygon3D?.InternalPoint3D();
                Vector3D normal = polygon3D?.GetPlane()?.Normal;
                if (point3D == null || normal == null)
                {
                    continue;
                }

                if (translation != null)
                {
                    point3D = point3D.GetMoved(translation) as Point3D;
                    if (point3D == null)
                    {
                        continue;
                    }
                }

                foreach (Panel panel in panels)
                {
                    Panel panel_Offset = OffsetMatch(panel, point3D, normal, halfWidth + tolerance, tolerance, out double distance);
                    if (panel_Offset != null && distance < distance_Min)
                    {
                        result = panel_Offset;
                        distance_Min = distance;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// <paramref name="panel"/> when a TAS surface at <paramref name="point3D"/> with normal <paramref name="normal"/> lies on
        /// it offset by up to <paramref name="maxDistance"/> along its normal - the building-element-width offset TAS applies with
        /// UseBEWidths ON - else null. Parallel normals (either sense) and the point, projected onto the panel, inside it within
        /// <paramref name="tolerance"/>. COM-free, so the rule is unit-tested.
        /// </summary>
        public static Panel OffsetMatch(Panel panel, Point3D point3D, Vector3D normal, double maxDistance, double tolerance, out double distance)
        {
            distance = double.NaN;

            Face3D face3D = panel?.GetFace3D(false);
            Plane plane = face3D?.GetPlane();
            if (plane == null || point3D == null || normal == null)
            {
                return null;
            }

            Vector3D normal_Unit = normal.Unit;
            Vector3D normal_Panel = plane.Normal?.Unit;
            if (normal_Unit == null || normal_Panel == null || Math.Abs(normal_Unit.DotProduct(normal_Panel)) < Math.Cos(Core.Tolerance.Angle))
            {
                return null;
            }

            distance = plane.Distance(point3D);
            if (double.IsNaN(distance) || distance > maxDistance)
            {
                return null;
            }

            Point3D point3D_Projected = plane.Closest(point3D);
            if (point3D_Projected == null || !InRangeOutsideHoles(face3D, point3D_Projected, tolerance))
            {
                return null;
            }

            return panel;
        }

        /// <summary>
        /// <see cref="Face3D.InRange(Point3D, double)"/> - on the face's plane and within its outer boundary, within
        /// <paramref name="tolerance"/> - and NOT inside one of its holes. <c>Face3D.InRange</c> tests the outer boundary only, so a
        /// surface filling a hole was matched to the holed panel: on the house model the GF Hall's stair-void Air surface was
        /// stamped onto the UF slab round it, and the Air panel itself was never stamped. A point on a hole's edge, within
        /// tolerance, still counts as on the face.
        /// </summary>
        public static bool InRangeOutsideHoles(Face3D face3D, Point3D point3D, double tolerance)
        {
            if (face3D == null || point3D == null || !face3D.InRange(point3D, tolerance))
            {
                return false;
            }

            List<Geometry.Planar.IClosed2D> internalEdge2Ds = face3D.InternalEdge2Ds;
            if (internalEdge2Ds == null || internalEdge2Ds.Count == 0)
            {
                return true;
            }

            Geometry.Planar.Point2D point2D = face3D.GetPlane().Convert(point3D);
            foreach (Geometry.Planar.IClosed2D internalEdge2D in internalEdge2Ds)
            {
                if (internalEdge2D != null && internalEdge2D.Inside(point2D, tolerance) && !internalEdge2D.On(point2D, tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        public static Aperture Match(this TBD.IZoneSurface zoneSurface, List<Aperture> apertures, out AperturePart aperturePart, double tolerance = Core.Tolerance.MacroDistance)
        {
            return Match(zoneSurface, apertures, null, out aperturePart, tolerance);
        }

        /// <summary>
        /// The binary-compatible zone-aware aperture match. Existing compiled callers bind to this exact CLR
        /// signature; translation-aware workflow code uses the overload that also accepts a vector.
        /// </summary>
        public static Aperture Match(this TBD.IZoneSurface zoneSurface, List<Aperture> apertures, string zoneGuid, out AperturePart aperturePart, double tolerance = Core.Tolerance.MacroDistance)
        {
            return Match(zoneSurface, apertures, zoneGuid, out aperturePart, tolerance, null);
        }

        /// <summary>
        /// The aperture, and which half of it, one zone surface belongs to - from the stamps first, geometry
        /// second.
        /// <para>
        /// <b><paramref name="zoneGuid"/> is what makes the stamp comparison an identity.</b> Same reasoning as
        /// <see cref="Match(TBD.IZoneSurface, List{Panel}, string, double, Vector3D)"/>: a surface number is scoped to
        /// its zone, so comparing numbers alone can hand a surface in one zone to an aperture in another. That
        /// matters more here than for a panel - after Stage 2 the two apertures involved may share a building
        /// element, a construction and an aperture type, so nothing downstream would look wrong.
        /// </para>
        /// <para>
        /// <b><paramref name="translation"/> compensates the gbXML route's coordinate shift</b>, exactly as in
        /// <see cref="Match(TBD.IZoneSurface, List{Panel}, string, double, Vector3D)"/>: TAS recentres the
        /// building footprint on the origin at gbXML import, so the surface's internal point is moved by this
        /// vector before it is tested against the (SAM-coordinate) aperture faces. Null keeps the previous
        /// behaviour.
        /// </para>
        /// </summary>
        public static Aperture Match(this TBD.IZoneSurface zoneSurface, List<Aperture> apertures, string zoneGuid, out AperturePart aperturePart, double tolerance, Vector3D translation)
        {
            aperturePart = Analytical.AperturePart.Undefined;

            if (zoneSurface == null || apertures == null || apertures.Count == 0)
            {
                return null;
            }

            TBD.buildingElement buildingElement = zoneSurface.buildingElement;
            if (buildingElement == null)
            {
                return null;
            }

            ApertureType apertureType = ApertureType(buildingElement.BEType);
            if (apertureType == Analytical.ApertureType.Undefined)
            {
                return null;
            }

            //Which HALF of an opening this surface is, read the way the import reads it - see
            //Query.AperturePart_BuildingElementType. NOT Query.AperturePart(int), which answers Frame for
            //BEType 14: that is TAS's DOOR type, and a door leaf is an opening's glazed half, not its frame.
            //With the wrong reading a door-typed surface was collected as a FRAME, so the aperture ended the
            //pass with two surfaces in its frame set - the real frame and the pane - bound to whichever of
            //the two elements was written last, and with no pane stamp at all. Modify.UpdateApertureDefinitions
            //then skipped the pane for want of a binding and Query.ApertureRebindKeys refused the frame,
            //because the set it claimed was not all on one element. TAS types an opening's pane DOOR rather
            //than GLAZING for a whole model at a time, so this is never one stray window: it is every window.
            aperturePart = AperturePart_BuildingElementType(buildingElement);
            if (aperturePart == Analytical.AperturePart.Undefined)
            {
                return null;
            }

            ApertureParameter apertureParameter_1 = ApertureZoneSurfaceReferenceParameter(aperturePart, 1);
            ApertureParameter apertureParameter_2 = ApertureZoneSurfaceReferenceParameter(aperturePart, 2);

            Core.Tas.ZoneSurfaceReference zoneSurfaceReference_Surface = new Core.Tas.ZoneSurfaceReference(zoneSurface.number, zoneGuid);

            foreach (Aperture aperture in apertures)
            {
                if (aperture == null)
                {
                    continue;
                }

                if (aperture.TryGetValue(apertureParameter_1, out Core.Tas.ZoneSurfaceReference zoneSurfaceReference_1) && zoneSurfaceReference_1 != null)
                {
                    if (ZoneSurfaceReferencesMatch(zoneSurfaceReference_1, zoneSurfaceReference_Surface))
                    {
                        return aperture;
                    }
                }

                if (aperture.TryGetValue(apertureParameter_2, out Core.Tas.ZoneSurfaceReference zoneSurfaceReference_2) && zoneSurfaceReference_2 != null)
                {
                    if (ZoneSurfaceReferencesMatch(zoneSurfaceReference_2, zoneSurfaceReference_Surface))
                    {
                        return aperture;
                    }
                }
            }

            List<TBD.IRoomSurface> roomSurfaces = zoneSurface.RoomSurfaces();
            if (roomSurfaces == null || roomSurfaces.Count == 0)
            {
                return null;
            }

            foreach (TBD.IRoomSurface roomSurface in roomSurfaces)
            {
                Polygon3D polygon3D = Geometry.Tas.Convert.ToSAM(roomSurface?.GetPerimeter()?.GetFace());
                if (polygon3D == null)
                {
                    continue;
                }

                Point3D point3D = polygon3D.InternalPoint3D();
                if (point3D == null)
                {
                    continue;
                }

                //The point is in TBD coordinates; the aperture faces are in SAM coordinates. On the gbXML
                //route TAS recentres the building footprint on the origin, so the two differ by a constant
                //translation which the caller (Modify.UpdateIds) has computed - move the point, never the
                //apertures.
                if (translation != null)
                {
                    point3D = point3D.GetMoved(translation) as Point3D;
                    if (point3D == null)
                    {
                        continue;
                    }
                }

                foreach (Aperture aperture in apertures)
                {
                    List<Face3D> face3Ds_AperturePart = aperture?.GetFace3Ds(aperturePart);
                    if(face3Ds_AperturePart != null)
                    {
                        foreach(Face3D face3D_AperturePart in face3Ds_AperturePart)
                        {
                            BoundingBox3D boundingBox3D = face3D_AperturePart.GetBoundingBox();
                            if (boundingBox3D == null)
                            {
                                continue;
                            }

                            // Bounding box vs. room-surface internal point; previously compared the
                            // aperture face bbox to itself (always true), defeating the prefilter.
                            if (!boundingBox3D.InRange(point3D, tolerance))
                            {
                                continue;
                            }

                            if (face3D_AperturePart.InRange(point3D, tolerance))
                            {
                                return aperture;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public static Aperture Match(this Core.Tas.ZoneSurfaceReference zoneSurfaceReference, IEnumerable<Aperture> apertures, out AperturePart aperturePart)
        {
            aperturePart = Analytical.AperturePart.Undefined;
            if (zoneSurfaceReference == null || apertures == null || !apertures.Any())
            {
                return null;
            }

            foreach(Aperture aperture in apertures)
            {
                //A null entry SKIPS. This returned, abandoning the whole search: one null in the list and
                //every aperture after it became unresolvable, so a surface that had a perfectly good stamp
                //further down the list silently matched nothing.
                if(aperture == null)
                {
                    continue;
                }

                Core.Tas.ZoneSurfaceReference zoneSurfaceReference_Temp = null;


                zoneSurfaceReference_Temp = null;
                if (aperture.TryGetValue(ApertureParameter.FrameZoneSurfaceReference_1, out zoneSurfaceReference_Temp) && zoneSurfaceReference_Temp != null)
                {
                    if(ZoneSurfaceReferencesMatch(zoneSurfaceReference_Temp, zoneSurfaceReference))
                    {
                        aperturePart = Analytical.AperturePart.Frame;
                        return aperture;
                    }
                }

                zoneSurfaceReference_Temp = null;
                if (aperture.TryGetValue(ApertureParameter.FrameZoneSurfaceReference_2, out zoneSurfaceReference_Temp) && zoneSurfaceReference_Temp != null)
                {
                    if (ZoneSurfaceReferencesMatch(zoneSurfaceReference_Temp, zoneSurfaceReference))
                    {
                        aperturePart = Analytical.AperturePart.Frame;
                        return aperture;
                    }
                }

                zoneSurfaceReference_Temp = null;
                if (aperture.TryGetValue(ApertureParameter.PaneZoneSurfaceReference_1, out zoneSurfaceReference_Temp) && zoneSurfaceReference_Temp != null)
                {
                    if (ZoneSurfaceReferencesMatch(zoneSurfaceReference_Temp, zoneSurfaceReference))
                    {
                        aperturePart = Analytical.AperturePart.Pane;
                        return aperture;
                    }
                }

                zoneSurfaceReference_Temp = null;
                if (aperture.TryGetValue(ApertureParameter.PaneZoneSurfaceReference_2, out zoneSurfaceReference_Temp) && zoneSurfaceReference_Temp != null)
                {
                    if (ZoneSurfaceReferencesMatch(zoneSurfaceReference_Temp, zoneSurfaceReference))
                    {
                        aperturePart = Analytical.AperturePart.Pane;
                        return aperture;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Whether two <see cref="Core.Tas.ZoneSurfaceReference"/>s name the same physical surface.
        /// <para>
        /// <b>SurfaceNumber alone is not unique across a building</b> - TAS numbers surfaces per zone, so
        /// zone A's surface 5 and zone B's surface 5 are different surfaces that happen to share a number.
        /// Comparing SurfaceNumber only, as this used to, matches the first same-numbered surface in
        /// whichever zone happens to be checked first - silently wrong whenever two zones' surface numbers
        /// overlap, which they routinely do.
        /// </para>
        /// <para>
        /// <b>ZoneGuid disambiguates it, when both sides carry one.</b> A reference missing a ZoneGuid (an
        /// older stamp, or one this export never set) falls back to SurfaceNumber alone, exactly as before -
        /// this is a strict tightening, never a new refusal: anything that matched before still matches:
        /// only a same-numbered surface in a DIFFERENT, GUID-stated zone stops matching.
        /// </para>
        /// <para>
        /// A standalone, non-overloaded public method (rather than folded into the heavily COM-overloaded
        /// <c>Match</c> family) deliberately - so it, and only the pure comparison the ZoneGuid fix actually
        /// is, can be exercised from a COM-free test project without pulling in the TBD/TAS3D interop every
        /// other <c>Match</c> overload needs.
        /// </para>
        /// </summary>
        public static bool ZoneSurfaceReferencesMatch(Core.Tas.ZoneSurfaceReference a, Core.Tas.ZoneSurfaceReference b)
        {
            if (a == null || b == null || a.SurfaceNumber != b.SurfaceNumber)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(a.ZoneGuid) || string.IsNullOrWhiteSpace(b.ZoneGuid))
            {
                return true;
            }

            //Normalised rather than compared raw, so this and ZoneSurfaceKey - the two places a physical
            //surface is compared - cannot disagree about whether two spellings of one GUID are one zone.
            return NormalizeZoneGuid(a.ZoneGuid) == NormalizeZoneGuid(b.ZoneGuid);
        }

        public static Construction Match(this TAS3D.Element element, IEnumerable<Construction> constructions)
        {
            if (constructions == null || element == null)
                return null;

            // Build the per-call lookup state, then delegate to the internal overload so the
            // single-call API stays equivalent to the previous in-line implementation.
            BuildConstructionLookup(constructions, out Dictionary<string, Construction> constructionsByName, out List<KeyValuePair<Construction, string>> constructions_Trimmed);
            return Match(element, constructionsByName, constructions_Trimmed);
        }

        /// <summary>
        /// Pre-built-state overload: callers that loop over many <see cref="TAS3D.Element"/>s
        /// against the same construction set should build the lookup tables once and reuse
        /// them across iterations to avoid per-call <c>.ToList()</c> + <c>RemoveAll</c> +
        /// <c>.Trim()</c> work. Use <see cref="BuildConstructionLookup(IEnumerable{Construction}, out Dictionary{string, Construction}, out List{KeyValuePair{Construction, string}})"/>
        /// to populate the inputs.
        /// </summary>
        internal static Construction Match(this TAS3D.Element element, Dictionary<string, Construction> constructionsByName, List<KeyValuePair<Construction, string>> constructions_Trimmed)
        {
            if (element == null || constructionsByName == null || constructions_Trimmed == null)
                return null;

            string name = Name(element);
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Pass 1: exact match on trimmed construction name.
            if (constructionsByName.TryGetValue(name, out Construction construction_Exact))
                return construction_Exact;

            // Pass 2: element name ends with ": {trimmedName}".
            foreach (KeyValuePair<Construction, string> entry in constructions_Trimmed)
            {
                if (name.EndsWith(string.Format(": {0}", entry.Value)))
                    return entry.Key;
            }

            // Pass 3: try the UniqueNameDecomposition-stripped name as exact match.
            if (UniqueNameDecomposition(element.name, out string prefix, out name, out Guid? guid, out int id))
            {
                if (constructionsByName.TryGetValue(name, out Construction construction_Decomposed))
                    return construction_Decomposed;
            }

            return null;
        }

        public static ApertureConstruction Match(this TAS3D.window window, IEnumerable<ApertureConstruction> apertureConstructions)
        {
            if (apertureConstructions == null || window == null)
                return null;

            BuildApertureConstructionLookup(apertureConstructions, out Dictionary<string, ApertureConstruction> apertureConstructionsByName, out List<KeyValuePair<ApertureConstruction, string>> apertureConstructions_Trimmed);
            return Match(window, apertureConstructionsByName, apertureConstructions_Trimmed);
        }

        /// <summary>
        /// Pre-built-state overload — see <see cref="Match(TAS3D.Element, Dictionary{string, Construction}, List{KeyValuePair{Construction, string}})"/>
        /// for the rationale.
        /// </summary>
        internal static ApertureConstruction Match(this TAS3D.window window, Dictionary<string, ApertureConstruction> apertureConstructionsByName, List<KeyValuePair<ApertureConstruction, string>> apertureConstructions_Trimmed)
        {
            if (window == null || apertureConstructionsByName == null || apertureConstructions_Trimmed == null)
                return null;

            string name = Name(window);
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (apertureConstructionsByName.TryGetValue(name, out ApertureConstruction apertureConstruction_Exact))
                return apertureConstruction_Exact;

            foreach (KeyValuePair<ApertureConstruction, string> entry in apertureConstructions_Trimmed)
            {
                if (name.EndsWith(string.Format(": {0}", entry.Value)))
                    return entry.Key;
            }

            if (UniqueNameDecomposition(window.name, out string prefix, out name, out Guid? guid, out int id))
            {
                if (apertureConstructionsByName.TryGetValue(name, out ApertureConstruction apertureConstruction_Decomposed))
                    return apertureConstruction_Decomposed;
            }

            return null;
        }

        /// <summary>
        /// Build a name → Construction lookup and a (Construction, trimmedName) list once,
        /// suitable for reuse across many <see cref="Match(TAS3D.Element, Dictionary{string, Construction}, List{KeyValuePair{Construction, string}})"/>
        /// calls. Mirrors the in-line filter that <see cref="Match(TAS3D.Element, IEnumerable{Construction})"/>
        /// used to do per-call. First-wins on duplicate trimmed names, matching the
        /// previous foreach order.
        /// </summary>
        internal static void BuildConstructionLookup(IEnumerable<Construction> constructions, out Dictionary<string, Construction> constructionsByName, out List<KeyValuePair<Construction, string>> constructions_Trimmed)
        {
            constructionsByName = new Dictionary<string, Construction>();
            constructions_Trimmed = new List<KeyValuePair<Construction, string>>();

            if (constructions == null)
                return;

            foreach (Construction construction in constructions)
            {
                if (construction == null || string.IsNullOrWhiteSpace(construction.Name))
                    continue;

                string trimmedName = construction.Name.Trim();
                constructions_Trimmed.Add(new KeyValuePair<Construction, string>(construction, trimmedName));

                if (!constructionsByName.ContainsKey(trimmedName))
                    constructionsByName[trimmedName] = construction;
            }
        }

        /// <summary>
        /// Mirror of <see cref="BuildConstructionLookup(IEnumerable{Construction}, out Dictionary{string, Construction}, out List{KeyValuePair{Construction, string}})"/>
        /// for <see cref="ApertureConstruction"/>.
        /// </summary>
        internal static void BuildApertureConstructionLookup(IEnumerable<ApertureConstruction> apertureConstructions, out Dictionary<string, ApertureConstruction> apertureConstructionsByName, out List<KeyValuePair<ApertureConstruction, string>> apertureConstructions_Trimmed)
        {
            apertureConstructionsByName = new Dictionary<string, ApertureConstruction>();
            apertureConstructions_Trimmed = new List<KeyValuePair<ApertureConstruction, string>>();

            if (apertureConstructions == null)
                return;

            foreach (ApertureConstruction apertureConstruction in apertureConstructions)
            {
                if (apertureConstruction == null || string.IsNullOrWhiteSpace(apertureConstruction.Name))
                    continue;

                string trimmedName = apertureConstruction.Name.Trim();
                apertureConstructions_Trimmed.Add(new KeyValuePair<ApertureConstruction, string>(apertureConstruction, trimmedName));

                if (!apertureConstructionsByName.ContainsKey(trimmedName))
                    apertureConstructionsByName[trimmedName] = apertureConstruction;
            }
        }
    }
}
