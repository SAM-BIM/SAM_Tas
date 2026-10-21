// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.InputEquivalence;
using EQ = SAM.Analytical.InputEquivalence.Query;
using SAM.Core;
using SAM.Geometry.Spatial;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests. It calls no SAM_Tas code:
// every expected value comes from raw stored SAM parameters, SAM geometry primitives and the S07 formulas of the
// merged evaluation core (S07 section 3, independence rule). SAM Calculated* queries appear only as cross-checks.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    public sealed class ExpectedLayer
    {
        public string Name;
        public double Thickness = double.NaN;
        public IMaterial Material;
    }

    public sealed class ExpectedAperture
    {
        public Guid Guid;
        public string Name;
        public Polygon3D Outer;
        public List<Polygon3D> Holes = new List<Polygon3D>();
        public double FaceArea = double.NaN;
        public ExpectedValue PaneArea;
        public ExpectedValue FrameArea;
        public ExpectedValue FramePercentage;
        public ExpectedValue FrameWidth;
        public string FrameRule;

        /// <summary>SAM's own Aperture.GetFace3Ds pane area - a cross-check only, never the expected value.</summary>
        public double CrossCheckPaneArea = double.NaN;
        public List<ExpectedLayer> PaneLayers = new List<ExpectedLayer>();
        public List<ExpectedLayer> FrameLayers = new List<ExpectedLayer>();
        public bool HasOpeningProperties;
        public string ConstructionName;
    }

    public sealed class ExpectedPanel
    {
        public Guid Guid;
        public string Name;
        public PanelType PanelType;
        public PanelGroup PanelGroup;
        public bool Adiabatic;
        public Polygon3D Outer;
        public List<ExpectedLayer> Layers = new List<ExpectedLayer>();
        public string ConstructionName;

        /// <summary>The panel's spaces in AdjacencyCluster.GetSpaces() order: the first sees the layers as listed (S07 1, UpdateReversed convention).</summary>
        public List<Guid> SpaceOrder = new List<Guid>();
        public List<ExpectedAperture> Apertures = new List<ExpectedAperture>();
    }

    public sealed class ExpectedProfile
    {
        /// <summary>The TBD slot the S07 row maps it to (ticLG, ticLL, ...).</summary>
        public string Slot;
        public ProfileType ProfileType;
        public string Name;
        public double[] RawValues;
        public ExpandedSchedule Expanded;
    }

    public sealed class ExpectedSpace
    {
        public Guid Guid;
        public string Name;
        public double StoredArea = double.NaN;
        public double StoredVolume = double.NaN;
        public double Occupancy = double.NaN;
        public List<ExpectedPanel> Panels = new List<ExpectedPanel>();
        public ExpectedValue FloorArea;
        public ExpectedValue ShellVolume;
        public bool HasInternalCondition;
        public string InternalConditionName;
        public Dictionary<string, double> Parameters = new Dictionary<string, double>(StringComparer.Ordinal);
        public Dictionary<string, ExpectedProfile> Profiles = new Dictionary<string, ExpectedProfile>(StringComparer.Ordinal);

        /// <summary>
        /// The Ventilation profile name the internal condition selects, as authored, or null when none is selected (blank
        /// counts as none). Kept apart from <see cref="Profiles"/>, which holds only profiles that resolve: a selection that
        /// does not resolve (a missing name, or a profile of another type) is still a selected delivery (D-F2), never "none".
        /// </summary>
        public string VentilationProfileSelection;

        /// <summary>SAM Calculated* values recorded beside the raw formulas, for the report only.</summary>
        public Dictionary<string, double> CrossChecks = new Dictionary<string, double>(StringComparer.Ordinal);
    }

    public sealed class ExpectedDesignDay
    {
        public string Role;
        public string Name;
        public int Year, Month, Day;
        public Dictionary<string, double[]> Hours = new Dictionary<string, double[]>(StringComparer.Ordinal);
    }

    public sealed class ExpectedWeather
    {
        public bool Present;
        public string Source;
        public string Name;
        public double Latitude = double.NaN, Longitude = double.NaN, Elevation = double.NaN, TimeZone = double.NaN, GroundTemperature = double.NaN;
        public int Year;
        public int DayCount;

        /// <summary>Weather series by TBD annual parameter index (0 dry bulb .. 6 wind direction), 8760 values; null when not 365 days.</summary>
        public Dictionary<int, double[]> Annual = new Dictionary<int, double[]>();
    }

    public sealed class ExpectedModel
    {
        public string FixtureId;
        public List<ExpectedSpace> Spaces = new List<ExpectedSpace>();
        public ExpectedWeather Weather = new ExpectedWeather();
        public string DesignDayRole;
        public string DesignDayRoleReason;
        public List<ExpectedDesignDay> CoolingDesignDays = new List<ExpectedDesignDay>();
        public List<ExpectedDesignDay> HeatingDesignDays = new List<ExpectedDesignDay>();

        /// <summary>D-FIX criteria the frozen model breaks (exhaust, opening properties, systems): reported, and never a PASS.</summary>
        public List<string> FixtureCriteria = new List<string>();
    }

    public static class ReadBackExpected
    {
        // The TBD annual weather parameter index and the SAM weather field the Tas writer puts there (SAM.Weather.Tas Update).
        public static readonly IReadOnlyList<KeyValuePair<int, WeatherDataType>> AnnualWeather = new[]
        {
            new KeyValuePair<int, WeatherDataType>(0, WeatherDataType.DryBulbTemperature),
            new KeyValuePair<int, WeatherDataType>(1, WeatherDataType.RelativeHumidity),
            new KeyValuePair<int, WeatherDataType>(2, WeatherDataType.GlobalSolarRadiation),
            new KeyValuePair<int, WeatherDataType>(3, WeatherDataType.DiffuseSolarRadiation),
            new KeyValuePair<int, WeatherDataType>(4, WeatherDataType.CloudCover),
            new KeyValuePair<int, WeatherDataType>(5, WeatherDataType.WindSpeed),
            new KeyValuePair<int, WeatherDataType>(6, WeatherDataType.WindDirection)
        };

        // The TBD WeatherDay field for each SAM weather field (design days).
        public static readonly IReadOnlyList<KeyValuePair<string, WeatherDataType>> DesignDayWeather = new[]
        {
            new KeyValuePair<string, WeatherDataType>("dryBulb", WeatherDataType.DryBulbTemperature),
            new KeyValuePair<string, WeatherDataType>("humidity", WeatherDataType.RelativeHumidity),
            new KeyValuePair<string, WeatherDataType>("globalRadiation", WeatherDataType.GlobalSolarRadiation),
            new KeyValuePair<string, WeatherDataType>("diffuseRadiation", WeatherDataType.DiffuseSolarRadiation),
            new KeyValuePair<string, WeatherDataType>("cloudCover", WeatherDataType.CloudCover),
            new KeyValuePair<string, WeatherDataType>("windSpeed", WeatherDataType.WindSpeed),
            new KeyValuePair<string, WeatherDataType>("windDirection", WeatherDataType.WindDirection)
        };

        // The S07 profile rows and the TBD slot each maps to (MA/Modify/UpdateInternalCondition.cs, cited by S07 rows 4-8).
        public static readonly IReadOnlyList<KeyValuePair<ProfileType, string>> ProfileSlots = new[]
        {
            new KeyValuePair<ProfileType, string>(ProfileType.Occupancy, "ticOSG"),
            new KeyValuePair<ProfileType, string>(ProfileType.Lighting, "ticLG"),
            new KeyValuePair<ProfileType, string>(ProfileType.EquipmentSensible, "ticESG"),
            new KeyValuePair<ProfileType, string>(ProfileType.EquipmentLatent, "ticELG"),
            new KeyValuePair<ProfileType, string>(ProfileType.Infiltration, "ticI"),
            new KeyValuePair<ProfileType, string>(ProfileType.Ventilation, "ticV"),
            new KeyValuePair<ProfileType, string>(ProfileType.Heating, "ticLL"),
            new KeyValuePair<ProfileType, string>(ProfileType.Cooling, "ticUL"),
            new KeyValuePair<ProfileType, string>(ProfileType.Humidification, "ticHLL"),
            new KeyValuePair<ProfileType, string>(ProfileType.Dehumidification, "ticHUL")
        };

        public static ExpectedModel Build(AnalyticalModel model, WeatherData suppliedWeather, string fixtureId)
        {
            ExpectedModel result = new ExpectedModel { FixtureId = fixtureId };
            AdjacencyCluster cluster = model?.AdjacencyCluster;
            if (cluster == null)
            {
                result.FixtureCriteria.Add("the frozen model has no adjacency cluster");
                return result;
            }

            List<Space> spaces = cluster.GetSpaces() ?? new List<Space>();
            Dictionary<Guid, int> spaceOrder = new Dictionary<Guid, int>();
            for (int i = 0; i < spaces.Count; i++) spaceOrder[spaces[i].Guid] = i;

            foreach (Space space in spaces)
            {
                result.Spaces.Add(BuildSpace(space, cluster, model.MaterialLibrary, model.ProfileLibrary, spaceOrder, result.FixtureCriteria));
            }

            BuildWeather(model, suppliedWeather, result.Weather);
            BuildDesignDays(model, suppliedWeather, result);

            if (cluster.GetObjects<global::SAM.Analytical.MechanicalSystem>()?.Count > 0)
            {
                result.FixtureCriteria.Add("D-FIX: the model carries mechanical systems (" + cluster.GetObjects<global::SAM.Analytical.MechanicalSystem>().Count + ")");
            }

            return result;
        }

        // ---- spaces and panels -------------------------------------------------------------------------------------------

        private static ExpectedSpace BuildSpace(Space space, AdjacencyCluster cluster, MaterialLibrary materials, ProfileLibrary profiles, Dictionary<Guid, int> spaceOrder, List<string> criteria)
        {
            ExpectedSpace result = new ExpectedSpace { Guid = space.Guid, Name = space.Name };
            result.StoredArea = Stored(space, global::SAM.Analytical.SpaceParameter.Area);
            result.StoredVolume = Stored(space, global::SAM.Analytical.SpaceParameter.Volume);
            result.Occupancy = Stored(space, global::SAM.Analytical.SpaceParameter.Occupancy);

            foreach (Panel panel in cluster.GetPanels(space) ?? new List<Panel>())
            {
                result.Panels.Add(BuildPanel(panel, cluster, materials, spaceOrder, criteria));
            }

            // The space's floor: its floor-group panels at the bottom of its shell, so a shared internal floor counts for the
            // space above it and not as the ceiling of the space below (S07 4, GeometricalFloorPanels intent, re-stated).
            double bottom = result.Panels.Where(x => x.Outer != null).SelectMany(x => x.Outer.GetPoints()).Select(x => x.Z).DefaultIfEmpty(double.NaN).Min();
            result.FloorArea = EQ.ExpectedFloorArea(result.Panels.Where(x => x.PanelGroup == PanelGroup.Floor && x.Outer != null && x.Outer.GetPoints().Max(p => p.Z) <= bottom + Core.Tolerance.MacroDistance).Select(x => x.Outer));
            result.ShellVolume = EQ.ExpectedShellVolume(result.Panels.Select(x => x.Outer));

            InternalCondition ic = space.InternalCondition;
            result.HasInternalCondition = ic != null;
            if (ic == null)
            {
                return result;
            }

            result.InternalConditionName = ic.Name;
            string ventilationSelection = ic.GetProfileName(ProfileType.Ventilation);
            result.VentilationProfileSelection = string.IsNullOrWhiteSpace(ventilationSelection) ? null : ventilationSelection;
            foreach (InternalConditionParameter parameter in new[]
            {
                InternalConditionParameter.AreaPerPerson, InternalConditionParameter.OccupancySensibleGainPerPerson, InternalConditionParameter.OccupancyLatentGainPerPerson,
                InternalConditionParameter.LightingGainPerArea, InternalConditionParameter.LightingGain, InternalConditionParameter.LightingGainPerPerson,
                InternalConditionParameter.EquipmentSensibleGainPerArea, InternalConditionParameter.EquipmentSensibleGain, InternalConditionParameter.EquipmentSensibleGainPerPerson,
                InternalConditionParameter.EquipmentLatentGainPerArea, InternalConditionParameter.EquipmentLatentGain,
                InternalConditionParameter.InfiltrationAirChangesPerHour,
                InternalConditionParameter.LightingRadiantProportion, InternalConditionParameter.OccupancyRadiantProportion, InternalConditionParameter.EquipmentRadiantProportion,
                InternalConditionParameter.SupplyAirFlow, InternalConditionParameter.SupplyAirFlowPerArea, InternalConditionParameter.SupplyAirFlowPerPerson, InternalConditionParameter.SupplyAirChangesPerHour,
                InternalConditionParameter.ExhaustAirFlow, InternalConditionParameter.ExhaustAirFlowPerArea, InternalConditionParameter.ExhaustAirFlowPerPerson, InternalConditionParameter.ExhaustAirChangesPerHour
            })
            {
                result.Parameters[parameter.ToString()] = ic.TryGetValue(parameter, out double value) ? value : double.NaN;
            }

            foreach (string exhaust in new[] { "ExhaustAirFlow", "ExhaustAirFlowPerArea", "ExhaustAirFlowPerPerson", "ExhaustAirChangesPerHour" })
            {
                double value = result.Parameters[exhaust];
                if (!double.IsNaN(value) && value != 0)
                {
                    criteria.Add("D-FIX: space '" + space.Name + "' states " + exhaust + " = " + value.ToString("R", CultureInfo.InvariantCulture) + " (exhaust is unsupported in Tas, S07 8c)");
                }
            }

            foreach (KeyValuePair<ProfileType, string> slot in ProfileSlots)
            {
                Profile profile = ic.GetProfile(slot.Key, profiles);
                if (profile == null)
                {
                    continue;
                }

                double[] raw = profile.GetValues();
                result.Profiles[slot.Value] = new ExpectedProfile { Slot = slot.Value, ProfileType = slot.Key, Name = profile.Name, RawValues = raw, Expanded = EQ.ExpandSchedule(raw) };
            }

            result.CrossChecks["CalculatedOccupancy"] = global::SAM.Analytical.Query.CalculatedOccupancy(space);
            result.CrossChecks["CalculatedLightingGain"] = global::SAM.Analytical.Query.CalculatedLightingGain(space);
            result.CrossChecks["CalculatedEquipmentSensibleGain"] = global::SAM.Analytical.Query.CalculatedEquipmentSensibleGain(space);
            result.CrossChecks["CalculatedEquipmentLatentGain"] = global::SAM.Analytical.Query.CalculatedEquipmentLatentGain(space);
            result.CrossChecks["OccupancySensibleGain"] = global::SAM.Analytical.Query.OccupancySensibleGain(space);
            result.CrossChecks["OccupancyLatentGain"] = global::SAM.Analytical.Query.OccupancyLatentGain(space);
            return result;
        }

        private static ExpectedPanel BuildPanel(Panel panel, AdjacencyCluster cluster, MaterialLibrary materials, Dictionary<Guid, int> spaceOrder, List<string> criteria)
        {
            ExpectedPanel result = new ExpectedPanel
            {
                Guid = panel.Guid,
                Name = panel.Name,
                PanelType = panel.PanelType,
                PanelGroup = global::SAM.Analytical.Query.PanelGroup(panel.PanelType),
                Adiabatic = panel.TryGetValue(global::SAM.Analytical.PanelParameter.Adiabatic, out bool adiabatic) && adiabatic,
                Outer = panel.GetFace3D()?.GetExternalEdge3D() as Polygon3D,
                ConstructionName = panel.Construction?.Name
            };

            foreach (ConstructionLayer layer in panel.Construction?.ConstructionLayers ?? new List<ConstructionLayer>())
            {
                result.Layers.Add(new ExpectedLayer { Name = layer.Name, Thickness = layer.Thickness, Material = materials?.GetMaterial(layer.Name) });
            }

            result.SpaceOrder = (cluster.GetRelatedObjects<Space>(panel) ?? new List<Space>()).Where(x => x != null && spaceOrder.ContainsKey(x.Guid)).OrderBy(x => spaceOrder[x.Guid]).Select(x => x.Guid).ToList();

            foreach (Aperture aperture in panel.Apertures ?? new List<Aperture>())
            {
                result.Apertures.Add(BuildAperture(aperture, materials, criteria));
            }

            return result;
        }

        private static ExpectedAperture BuildAperture(Aperture aperture, MaterialLibrary materials, List<string> criteria)
        {
            Face3D face = aperture.GetFace3D();
            ApertureConstruction construction = aperture.ApertureConstruction;
            ExpectedAperture result = new ExpectedAperture
            {
                Guid = aperture.Guid,
                Name = aperture.Name,
                Outer = face?.GetExternalEdge3D() as Polygon3D,
                Holes = face?.GetInternalEdge3Ds()?.OfType<Polygon3D>().ToList() ?? new List<Polygon3D>(),
                ConstructionName = construction?.Name,
                HasOpeningProperties = aperture.TryGetValue(global::SAM.Analytical.ApertureParameter.OpeningProperties, out IOpeningProperties openingProperties) && openingProperties != null
            };

            if (result.HasOpeningProperties)
            {
                criteria.Add("D-FIX: aperture " + aperture.Guid + " carries OpeningProperties (the equivalence fixtures are closed-window)");
            }

            foreach (ConstructionLayer layer in construction?.PaneConstructionLayers ?? new List<ConstructionLayer>())
            {
                result.PaneLayers.Add(new ExpectedLayer { Name = layer.Name, Thickness = layer.Thickness, Material = materials?.GetMaterial(layer.Name) });
            }

            foreach (ConstructionLayer layer in construction?.FrameConstructionLayers ?? new List<ConstructionLayer>())
            {
                result.FrameLayers.Add(new ExpectedLayer { Name = layer.Name, Thickness = layer.Thickness, Material = materials?.GetMaterial(layer.Name) });
            }

            result.FaceArea = face == null ? double.NaN : face.GetArea();
            FrameSplit(result, construction, face);

            double frameThickness = result.FrameLayers.Count == 0 ? 0 : result.FrameLayers.Sum(x => x.Thickness);
            double defaultFrameWidth = construction != null && construction.TryGetValue(ApertureConstructionParameter.DefaultFrameWidth, out double width) ? width : double.NaN;
            result.FrameWidth = construction == null ? ExpectedValue.Unspecified("no aperture construction") : EQ.ExpectedFrameWidth(frameThickness, defaultFrameWidth);

            try
            {
                List<Face3D> panes = aperture.GetFace3Ds(AperturePart.Pane);
                result.CrossCheckPaneArea = panes == null ? double.NaN : panes.Sum(x => x.GetArea());
            }
            catch (Exception)
            {
                result.CrossCheckPaneArea = double.NaN;
            }

            return result;
        }

        /// <summary>
        /// The S07 T-FRAME split of an aperture face into pane and frame (S07 section 5, from SA/Classes/Aperture.cs:119-198),
        /// recomputed here from the raw geometry: no frame layers - all pane; authored holes - the frame is the face with
        /// holes, the panes are the holes; otherwise the pane is the face offset inwards by DefaultFrameWidth (present,
        /// non-zero, not NaN) else by the frame thickness, and no frame when that offset leaves no pane or the thickness is
        /// 0 or NaN. The inward offset is implemented for convex polygons only; anything else is S-INVALID, never a guess.
        /// </summary>
        private static void FrameSplit(ExpectedAperture result, ApertureConstruction construction, Face3D face)
        {
            if (face == null || double.IsNaN(result.FaceArea) || result.FaceArea <= 0)
            {
                result.PaneArea = result.FrameArea = result.FramePercentage = ExpectedValue.Invalid("the aperture has no face");
                result.FrameRule = "no face";
                return;
            }

            double net = result.FaceArea;
            if (result.FrameLayers.Count == 0)
            {
                Set(result, net, 0, "no frame layers: the whole face is pane");
                return;
            }

            if (result.Holes.Count > 0)
            {
                double holes = result.Holes.Sum(x => x.GetArea());
                Set(result, holes, net, "authored holes: the panes are the holes, the frame is the face with holes");
                return;
            }

            double thickness = result.FrameLayers.Sum(x => x.Thickness);
            if (double.IsNaN(thickness) || thickness == 0)
            {
                Set(result, net, 0, "frame thickness 0 or NaN: no frame");
                return;
            }

            double offset = construction != null && construction.TryGetValue(ApertureConstructionParameter.DefaultFrameWidth, out double defaultFrameWidth) && !double.IsNaN(defaultFrameWidth) && defaultFrameWidth != 0 ? defaultFrameWidth : thickness;
            double pane = InwardOffsetArea(result.Outer, offset, out string problem);
            if (problem != null)
            {
                result.PaneArea = result.FrameArea = result.FramePercentage = ExpectedValue.Invalid(problem);
                result.FrameRule = problem;
                return;
            }

            if (double.IsNaN(pane) || pane <= 0)
            {
                Set(result, net, 0, "the offset leaves no pane: no frame");
                return;
            }

            Set(result, pane, net - pane, "pane = face offset inwards by " + offset.ToString("R", CultureInfo.InvariantCulture) + " m");
        }

        private static void Set(ExpectedAperture result, double pane, double frame, string rule)
        {
            result.PaneArea = ExpectedValue.FromValue(pane);
            result.FrameArea = ExpectedValue.FromValue(frame);
            result.FramePercentage = EQ.ExpectedFramePercentage(frame, pane);
            result.FrameRule = rule;
        }

        /// <summary>The area of a convex planar polygon moved inwards by <paramref name="offset"/>; NaN when nothing is left.</summary>
        public static double InwardOffsetArea(Polygon3D polygon, double offset, out string problem)
        {
            problem = null;
            List<Point3D> points = polygon?.GetPoints();
            if (points == null || points.Count < 3)
            {
                problem = "the aperture outline is missing";
                return double.NaN;
            }

            Plane plane = polygon.GetPlane();
            List<Geometry.Planar.Point2D> p = points.ConvertAll(x => plane.Convert(x));
            int n = p.Count;

            double signed = 0;
            for (int i = 0; i < n; i++)
            {
                signed += p[i].X * p[(i + 1) % n].Y - p[(i + 1) % n].X * p[i].Y;
            }

            double orientation = System.Math.Sign(signed);
            for (int i = 0; i < n; i++)
            {
                Geometry.Planar.Point2D a = p[i], b = p[(i + 1) % n], c = p[(i + 2) % n];
                double cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (cross * orientation < -1e-12)
                {
                    problem = "the aperture outline is not convex: the inward frame offset is not implemented for it in PR-B";
                    return double.NaN;
                }
            }

            // Each edge moved inwards; each new vertex is the intersection of the two moved edges meeting there.
            List<double[]> lines = new List<double[]>();
            for (int i = 0; i < n; i++)
            {
                Geometry.Planar.Point2D a = p[i], b = p[(i + 1) % n];
                double dx = b.X - a.X, dy = b.Y - a.Y, length = System.Math.Sqrt(dx * dx + dy * dy);
                if (length < 1e-12)
                {
                    problem = "the aperture outline has a zero-length edge";
                    return double.NaN;
                }

                // Inward normal for a counter-clockwise polygon is (-dy, dx).
                double nx = -dy / length * orientation, ny = dx / length * orientation;
                lines.Add(new[] { a.X + nx * offset, a.Y + ny * offset, dx, dy });
            }

            List<double[]> moved = new List<double[]>();
            for (int i = 0; i < n; i++)
            {
                double[] l1 = lines[(i + n - 1) % n], l2 = lines[i];
                double det = l1[2] * l2[3] - l1[3] * l2[2];
                if (System.Math.Abs(det) < 1e-12)
                {
                    problem = "two consecutive aperture edges are parallel";
                    return double.NaN;
                }

                double t = ((l2[0] - l1[0]) * l2[3] - (l2[1] - l1[1]) * l2[2]) / det;
                moved.Add(new[] { l1[0] + t * l1[2], l1[1] + t * l1[3] });
            }

            double area = 0;
            for (int i = 0; i < n; i++)
            {
                area += moved[i][0] * moved[(i + 1) % n][1] - moved[(i + 1) % n][0] * moved[i][1];
            }

            area /= 2;

            // A collapsed offset flips the orientation; each moved edge must keep its original direction.
            if (System.Math.Sign(area) != orientation)
            {
                return double.NaN;
            }

            for (int i = 0; i < n; i++)
            {
                double ex = moved[(i + 1) % n][0] - moved[i][0], ey = moved[(i + 1) % n][1] - moved[i][1];
                if (ex * lines[i][2] + ey * lines[i][3] <= 0)
                {
                    return double.NaN;
                }
            }

            return System.Math.Abs(area);
        }

        private static double Stored(Space space, global::SAM.Analytical.SpaceParameter parameter)
        {
            return space.TryGetValue(parameter, out double value) ? value : double.NaN;
        }

        // ---- weather and design days ---------------------------------------------------------------------------------------

        private static void BuildWeather(AnalyticalModel model, WeatherData supplied, ExpectedWeather result)
        {
            // S07 section 3, Tas weather: the WeatherData passed to the workflow, else the model's.
            WeatherData weather = supplied;
            result.Source = "supplied (frozen weather.json)";
            if (weather == null && model.TryGetValue(global::SAM.Analytical.AnalyticalModelParameter.WeatherData, out WeatherData weather_Model))
            {
                weather = weather_Model;
                result.Source = "model global::SAM.Analytical.AnalyticalModelParameter.WeatherData";
            }

            if (weather == null)
            {
                result.Present = false;
                result.Source = "none";
                return;
            }

            result.Present = true;
            result.Name = weather.Name;
            result.Latitude = weather.Latitude;
            result.Longitude = weather.Longitude;
            result.Elevation = weather.Elevtion;

            if (weather.TryGetValue(WeatherDataParameter.TimeZone, out string timeZone) && !string.IsNullOrWhiteSpace(timeZone))
            {
                result.TimeZone = double.TryParse(timeZone, NumberStyles.Float, CultureInfo.InvariantCulture, out double hours) ? hours : Core.Query.Double(Core.Query.UTC(timeZone));
            }

            // The S07 9.ground reference: the 2 m depth series when present, else the first; the mean of its 12 months.
            if (weather.TryGetValue(WeatherDataParameter.GroundTemperatures, out SAMCollection<GroundTemperature> grounds) && grounds != null && grounds.Count != 0)
            {
                GroundTemperature ground = grounds.ToList().Find(x => System.Math.Abs(x.Depth - 2.0) < Core.Tolerance.MacroDistance) ?? grounds.FirstOrDefault();
                if (ground?.Temperatures != null && ground.Temperatures.Length == 12)
                {
                    result.GroundTemperature = ground.Temperatures.Sum() / 12.0;
                }
            }

            WeatherYear year = weather.WeatherYears?.FirstOrDefault();
            if (year == null)
            {
                return;
            }

            result.Year = year.Year;
            result.DayCount = year.WeatherDays?.Count(x => x != null) ?? 0;
            if (result.DayCount != 365)
            {
                return;
            }

            foreach (KeyValuePair<int, WeatherDataType> item in AnnualWeather)
            {
                double[] values = new double[8760];
                for (int d = 0; d < 365; d++)
                {
                    for (int h = 0; h < 24; h++)
                    {
                        values[d * 24 + h] = year[d][item.Value, h];
                    }
                }

                result.Annual[item.Key] = values;
            }
        }

        private static void BuildDesignDays(AnalyticalModel model, WeatherData supplied, ExpectedModel result)
        {
            // S07 section 3, Tas design days (MA/Query/DesignDays_Authoritative.cs:45-83), re-stated here: caller lists (the
            // harness supplies none) -> days derived from the supplied weather -> the model's lists. Never merged.
            List<DesignDay> cooling = null, heating = null;
            string role = null, reason = null;
            if (supplied != null)
            {
                DesignDay c = global::SAM.Analytical.Query.CoolingDesignDay(supplied);
                DesignDay h = global::SAM.Analytical.Query.HeatingDesignDay(supplied);
                cooling = c == null ? null : new List<DesignDay> { c };
                heating = h == null ? null : new List<DesignDay> { h };
                role = "weather-derived";
                reason = "no caller lists; weather was supplied to the run, so its derived days are authoritative (SA/Query/CoolingDesignDay.cs, HeatingDesignDay.cs)";
            }

            if (cooling == null && model.TryGetValue(global::SAM.Analytical.AnalyticalModelParameter.CoolingDesignDays, out SAMCollection<DesignDay> coolingModel) && coolingModel?.Count > 0)
            {
                cooling = coolingModel.ToList();
                role = role == null ? "authored" : role + "+authored(cooling)";
                reason = (reason ?? "no caller lists and no supplied weather") + "; cooling from the model's list";
            }

            if (heating == null && model.TryGetValue(global::SAM.Analytical.AnalyticalModelParameter.HeatingDesignDays, out SAMCollection<DesignDay> heatingModel) && heatingModel?.Count > 0)
            {
                heating = heatingModel.ToList();
                role = role == null ? "authored" : role + "+authored(heating)";
                reason = (reason ?? "no caller lists and no supplied weather") + "; heating from the model's list";
            }

            result.DesignDayRole = role ?? "none";
            result.DesignDayRoleReason = reason ?? "no design day is stated or derivable";
            foreach (DesignDay d in cooling ?? new List<DesignDay>()) result.CoolingDesignDays.Add(DesignDayOf(d, "cooling"));
            foreach (DesignDay d in heating ?? new List<DesignDay>()) result.HeatingDesignDays.Add(DesignDayOf(d, "heating"));
        }

        private static ExpectedDesignDay DesignDayOf(DesignDay designDay, string role)
        {
            ExpectedDesignDay result = new ExpectedDesignDay { Role = role, Name = designDay.Name, Year = designDay.Year, Month = designDay.Month, Day = designDay.Day };
            foreach (KeyValuePair<string, WeatherDataType> item in DesignDayWeather)
            {
                double[] values = new double[24];
                for (int h = 0; h < 24; h++)
                {
                    values[h] = designDay[item.Value, h];
                }

                result.Hours[item.Key] = values;
            }

            return result;
        }
    }
}
