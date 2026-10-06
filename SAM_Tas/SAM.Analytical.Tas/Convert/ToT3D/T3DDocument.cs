// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using TAS3D;

namespace SAM.Analytical.Tas
{
    /// <summary>
    /// The direct SAM -> T3D route: SAM geometry into a TAS3D document through TAS's own geometry importer
    /// (<c>T3DDocument.CreateIDFImport</c> / <c>WrImportIDF</c>), with no gbXML anywhere.
    /// <para>
    /// <b>The order is not negotiable</b> (measured, Tas 9.5.7): <c>Create</c>; elements, window types, the zone
    /// set and its zones; <c>CreateIDFImport</c>; the <c>Add*</c> calls; <c>CreateImportedModel</c>. An opening
    /// attaches to the surface added <i>last</i>, so each is emitted straight after its host - the plan
    /// (<see cref="Query.T3DImportPlan"/>) already lists them in that order, and this replay keeps it.
    /// </para>
    /// </summary>
    public static partial class Convert
    {
        /// <summary>
        /// Converts a SAM model to a new T3D at <paramref name="path_T3D"/>: creates the document, runs the direct
        /// conversion, saves, and closes it.
        /// </summary>
        public static bool ToT3D(this AnalyticalModel analyticalModel, string path_T3D, ToT3DOptions options, out T3DImportReport report)
        {
            report = null;

            if (analyticalModel == null || string.IsNullOrWhiteSpace(path_T3D))
            {
                return false;
            }

            using (SAMT3DDocument sAMT3DDocument = new SAMT3DDocument())
            {
                bool result = ToT3D(analyticalModel, sAMT3DDocument.T3DDocument, options, out report);
                if (result)
                {
                    result = sAMT3DDocument.Save(path_T3D);
                }

                return result;
            }
        }

        /// <summary>
        /// Converts a SAM model into <paramref name="t3DDocument"/>, which is <b>created afresh</b> (<c>Create</c>)
        /// - it must not hold a model already. The document is left open and unsaved, so a caller can add to it
        /// (location, GUID) before saving or exporting.
        /// </summary>
        /// <param name="analyticalModel">The SAM model.</param>
        /// <param name="t3DDocument">The TAS3D document to build the model in.</param>
        /// <param name="options">What to do; null means the defaults.</param>
        /// <param name="report">What was imported, and everything that was not.</param>
        /// <returns>True when TAS built the model; false when the conversion could not run, or TAS refused it.</returns>
        public static bool ToT3D(this AnalyticalModel analyticalModel, T3DDocument t3DDocument, ToT3DOptions options, out T3DImportReport report)
        {
            report = null;

            if (analyticalModel == null || t3DDocument == null)
            {
                return false;
            }

            options = options ?? new ToT3DOptions();

            T3DImportPlan plan = analyticalModel.T3DImportPlan(options);
            report = plan?.Report;
            if (plan == null)
            {
                return false;
            }

            Location location = analyticalModel.Location;

            return ToT3D(plan, t3DDocument, options, location);
        }

        /// <summary>
        /// Replays a plan into a freshly created <paramref name="t3DDocument"/>. The plan's report is completed
        /// with what TAS accepted and what it did not.
        /// </summary>
        public static bool ToT3D(this T3DImportPlan plan, T3DDocument t3DDocument, ToT3DOptions options, Location location = null)
        {
            if (plan == null || t3DDocument == null)
            {
                return false;
            }

            options = options ?? new ToT3DOptions();
            T3DImportReport report = plan.Report;
            report.Success = false;

            // COM wrappers handed out by TAS, released together on the way out.
            List<object> comObjects = new List<object>();
            WrImportIDF wrImportIDF = null;

            try
            {
                t3DDocument.Create();

                TAS3D.Building building = t3DDocument.Building;
                comObjects.Add(building);

                // ---- Building ---------------------------------------------------------------------------
                if (!string.IsNullOrWhiteSpace(plan.Name))
                {
                    building.name = plan.Name;
                }

                if (!string.IsNullOrWhiteSpace(plan.Description))
                {
                    building.description = plan.Description;
                }

                if (!double.IsNaN(plan.NorthAngle))
                {
                    building.northAngle = plan.NorthAngle;
                }

                if (location != null)
                {
                    building.longitude = location.Longitude;
                    building.latitude = location.Latitude;

                    if (location.TryGetValue(LocationParameter.TimeZone, out string timeZone))
                    {
                        double @double = Core.Query.Double(Core.Query.UTC(timeZone));
                        if (!double.IsNaN(@double))
                        {
                            building.timeZone = @double;
                        }
                    }
                }

                // ---- Elements ---------------------------------------------------------------------------
                Dictionary<string, TAS3D.Element> elements = new Dictionary<string, TAS3D.Element>();
                foreach (T3DElementSpec spec in plan.Elements)
                {
                    TAS3D.Element element = building.AddElement(spec.Name, spec.Colour, spec.Width);
                    if (element == null)
                    {
                        report.Skipped.Add(string.Format("Element '{0}': TAS refused to create it, so every panel on it was not imported.", spec.Name));
                        continue;
                    }

                    comObjects.Add(element);

                    element.transparent = spec.Transparent;
                    element.internalShadows = spec.InternalShadows;
                    element.ground = spec.Ground;
                    element.ghost = spec.Ghost;
                    element.zoneFloorArea = spec.ZoneFloorArea;

                    if (spec.BEType != -1)
                    {
                        element.BEType = spec.BEType;
                    }

                    if (!string.IsNullOrWhiteSpace(spec.Description))
                    {
                        element.description = spec.Description;
                    }

                    elements[spec.Key] = element;
                }

                // ---- Window types -----------------------------------------------------------------------
                Dictionary<string, window> windows = new Dictionary<string, window>();
                foreach (T3DWindowSpec spec in plan.Windows)
                {
                    window window = building.AddWindow(spec.Name, spec.OpeningType, spec.Colour, spec.Height, spec.Width, spec.Level);
                    if (window == null)
                    {
                        report.Skipped.Add(string.Format("Window type '{0}': TAS refused to create it, so every opening on it was not imported.", spec.Name));
                        continue;
                    }

                    comObjects.Add(window);

                    if (spec.PositionType != -1)
                    {
                        window.positionType = spec.PositionType;
                    }

                    window.transparent = spec.Transparent;
                    window.internalShadows = spec.InternalShadows;

                    if (!double.IsNaN(spec.FrameWidth))
                    {
                        window.frameWidth = spec.FrameWidth;
                    }

                    if (!double.IsNaN(spec.FramePercent))
                    {
                        window.isPercFrame = true;
                        window.framePerc = spec.FramePercent;
                    }

                    if (!string.IsNullOrWhiteSpace(spec.Description))
                    {
                        window.description = spec.Description;
                    }

                    windows[spec.Key] = window;
                }

                // ---- Zones ------------------------------------------------------------------------------
                zoneSet zoneSet = building.AddZoneSet(options.ZoneSetName, string.Empty, 0);
                comObjects.Add(zoneSet);

                List<TAS3D.Zone> zones = new List<TAS3D.Zone>();
                foreach (T3DZoneSpec spec in plan.Zones)
                {
                    TAS3D.Zone zone = zoneSet.AddZone();
                    comObjects.Add(zone);

                    zone.name = spec.Name;
                    zone.description = spec.Description;
                    zone.external = spec.External;

                    if (spec.Colour.HasValue)
                    {
                        zone.colour = spec.Colour.Value;
                    }

                    zones.Add(zone);
                }

                // ---- Surfaces, openings, shades ----------------------------------------------------------
                wrImportIDF = (WrImportIDF)t3DDocument.CreateIDFImport();

                //Before any Add*: set on the importer it governs how the polygons are read. (On the document
                //before CreateIDFImport it has no effect - measured - so it is set on both sides of the import.)
                wrImportIDF.SetUseBEWidths(options.UseWidths);

                bool failed = false;
                foreach (T3DSurfaceSpec surface in plan.Surfaces)
                {
                    if (!elements.TryGetValue(surface.ElementKey, out TAS3D.Element element))
                    {
                        report.Skipped.Add(string.Format("Panel '{0}' ({1}): its element was not created, so the surface was not imported.", surface.PanelName, surface.PanelGuid));
                        continue;
                    }

                    try
                    {
                        switch (surface.Kind)
                        {
                            case T3DSurfaceKind.Internal:
                                wrImportIDF.AddInternalSurface(zones[surface.Zone], zones[surface.Zone2], element, surface.ReverseElement, surface.Coordinates);
                                break;

                            case T3DSurfaceKind.Adiabatic:
                            case T3DSurfaceKind.InternalAdiabaticSide:
                                wrImportIDF.AddSurface(zones[surface.Zone], element, surface.ReverseElement, true, surface.Coordinates);
                                break;

                            default:
                                wrImportIDF.AddSurface(zones[surface.Zone], element, surface.ReverseElement, false, surface.Coordinates);
                                break;
                        }
                    }
                    catch (Exception exception)
                    {
                        report.Skipped.Add(string.Format("Panel '{0}' ({1}): TAS rejected the surface ({2}); its openings were not imported either.", surface.PanelName, surface.PanelGuid, exception.Message));
                        failed = true;
                        continue;
                    }

                    //AddOpening attaches to the surface added last - so nothing may be added between the host
                    //and its openings.
                    foreach (T3DOpeningSpec opening in surface.Openings)
                    {
                        if (!windows.TryGetValue(opening.WindowKey, out window window))
                        {
                            report.Skipped.Add(string.Format("Aperture {0} on panel '{1}': its window type was not created, so the opening was not imported.", opening.ApertureGuid, surface.PanelName));
                            continue;
                        }

                        try
                        {
                            wrImportIDF.AddOpening(window, opening.Coordinates);
                        }
                        catch (Exception exception)
                        {
                            report.Skipped.Add(string.Format("Aperture {0} on panel '{1}': TAS rejected the opening ({2}).", opening.ApertureGuid, surface.PanelName, exception.Message));
                            failed = true;
                        }
                    }
                }

                foreach (T3DShadeSpec shade in plan.Shades)
                {
                    if (!elements.TryGetValue(shade.ElementKey, out TAS3D.Element element))
                    {
                        report.Skipped.Add(string.Format("Shade '{0}' ({1}): its element was not created, so it was not imported.", shade.PanelName, shade.PanelGuid));
                        continue;
                    }

                    try
                    {
                        wrImportIDF.AddShadeSurface(element, shade.ReverseElement, shade.Coordinates);
                    }
                    catch (Exception exception)
                    {
                        report.Skipped.Add(string.Format("Shade '{0}' ({1}): TAS rejected it ({2}).", shade.PanelName, shade.PanelGuid, exception.Message));
                        failed = true;
                    }
                }

                bool created = wrImportIDF.CreateImportedModel();

                t3DDocument.SetUseBEWidths(options.UseWidths);

                report.Success = created && !failed;
                if (!created)
                {
                    report.Skipped.Add("TAS reported that it could not create the imported model (CreateImportedModel returned false).");
                }

                return report.Success;
            }
            finally
            {
                if (wrImportIDF != null)
                {
                    Core.Modify.ReleaseCOMObject(wrImportIDF);
                }

                foreach (object comObject in comObjects)
                {
                    Core.Modify.ReleaseCOMObject(comObject);
                }
            }
        }
    }
}
