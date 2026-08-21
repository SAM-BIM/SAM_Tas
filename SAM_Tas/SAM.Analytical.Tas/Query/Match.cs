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
            if (zoneSurface == null || panels == null  || panels.Count == 0)
            {
                return null;
            }

            foreach (Panel panel in panels)
            {
                if (panel == null)
                {
                    continue;
                }

                if (panel.TryGetValue(PanelParameter.ZoneSurfaceReference_1, out Core.Tas.ZoneSurfaceReference zoneSurfaceReference_1) && zoneSurfaceReference_1 != null)
                {
                    if(zoneSurfaceReference_1.SurfaceNumber == zoneSurface.number)
                    {
                        return panel;
                    }
                }

                if (panel.TryGetValue(PanelParameter.ZoneSurfaceReference_2, out Core.Tas.ZoneSurfaceReference zoneSurfaceReference_2) && zoneSurfaceReference_2 != null)
                {
                    if (zoneSurfaceReference_1.SurfaceNumber == zoneSurface.number)
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

                    if (face3D.InRange(point3D, tolerance))
                    {
                        return panel;
                    }
                }
            }

            return null;
        }

        public static Aperture Match(this TBD.IZoneSurface zoneSurface, List<Aperture> apertures, out AperturePart aperturePart, double tolerance = Core.Tolerance.MacroDistance)
        {
            aperturePart = Analytical.AperturePart.Undefined;

            if (zoneSurface == null || apertures == null || apertures.Count == 0)
            {
                return null;
            }

            TBD.buildingElement buildingElement = zoneSurface.buildingElement;

            ApertureType apertureType = ApertureType(buildingElement.BEType);
            if (apertureType == Analytical.ApertureType.Undefined)
            {
                return null;
            }

            aperturePart = AperturePart(buildingElement.BEType);
            if (aperturePart == Analytical.AperturePart.Undefined)
            {
                return null;
            }

            ApertureParameter apertureParameter_1 = aperturePart == Analytical.AperturePart.Frame ? ApertureParameter.FrameZoneSurfaceReference_1 : ApertureParameter.PaneZoneSurfaceReference_1;
            ApertureParameter apertureParameter_2 = aperturePart == Analytical.AperturePart.Frame ? ApertureParameter.FrameZoneSurfaceReference_2 : ApertureParameter.PaneZoneSurfaceReference_2;

            foreach (Aperture aperture in apertures)
            {
                if (aperture == null)
                {
                    continue;
                }

                if (aperture.TryGetValue(apertureParameter_1, out Core.Tas.ZoneSurfaceReference zoneSurfaceReference_1) && zoneSurfaceReference_1 != null)
                {
                    if (zoneSurface.number == zoneSurfaceReference_1.SurfaceNumber)
                    {
                        return aperture;
                    }
                }

                if (aperture.TryGetValue(apertureParameter_2, out Core.Tas.ZoneSurfaceReference zoneSurfaceReference_2) && zoneSurfaceReference_2 != null)
                {
                    if (zoneSurface.number == zoneSurfaceReference_2.SurfaceNumber)
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
                if(aperture == null)
                {
                    return null;
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

            return a.ZoneGuid == b.ZoneGuid;
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