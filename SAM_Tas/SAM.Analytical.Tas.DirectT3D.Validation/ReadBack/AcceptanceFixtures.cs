// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>
    /// The S08 PR-C equivalence fixtures (D-FIX), authored here and nowhere else. They are written once by
    /// <c>readback-fixtures</c>, committed with their sha256 and provenance (<c>ReadBack/Fixtures/</c>), and the committed
    /// files, not a fresh call, are what an acceptance run exports.
    /// <para>
    /// Independent of <see cref="CalibrationModels"/>: no geometry, material, construction, profile or gain value is shared
    /// with the models the PR-B rules were measured on, so a fixture verdict is not the calibration's self-consistency.
    /// </para>
    /// <para>
    /// Declared panel convention for both: <b>centre-line (mid-surface)</b> panels, every panel carrying
    /// <c>PanelSourceParameter.PhysicalThickness</c> equal to its construction's layer total, as PanelsBySolid output does.
    /// Stored Area and Volume are the centre-line geometry (D-F9 C: stored = geometric; the intended volume is the
    /// centre-line shell). Closed windows (no OpeningProperties), no exhaust, no systems, no IZAM data, every S07 gain,
    /// fraction, setpoint, humidistat and infiltration authored, single-pane glazing (no gas layer).
    /// </para>
    /// </summary>
    public static class AcceptanceFixtures
    {
        public const string Box = "s08-fix-box";
        public const string TwoZones = "s08-fix-two";

        public static IReadOnlyList<string> Names { get; } = new[] { Box, TwoZones };

        public const string PanelConvention = "centre-line (mid-surface) panels with PhysicalThickness = construction layer total";

        public static AnalyticalModel Create(string name)
        {
            switch (name)
            {
                case Box:
                    return CreateBox();

                case TwoZones:
                    return CreateTwoZones();

                default:
                    return null;
            }
        }

        // ---- materials (inside-to-outside layer order, as SAM lists a construction from its first space) ---------------

        private static OpaqueMaterial Opaque(string name, double conductivity, double specificHeat, double density, double extSolar, double intSolar, double extLight, double intLight, double extEmissivity, double intEmissivity, double vapour)
        {
            OpaqueMaterial result = new OpaqueMaterial(name, "S08 PR-C fixture", name, name, conductivity, specificHeat, density);
            result.SetValue(OpaqueMaterialParameter.ExternalSolarReflectance, extSolar);
            result.SetValue(OpaqueMaterialParameter.InternalSolarReflectance, intSolar);
            result.SetValue(OpaqueMaterialParameter.ExternalLightReflectance, extLight);
            result.SetValue(OpaqueMaterialParameter.InternalLightReflectance, intLight);
            result.SetValue(OpaqueMaterialParameter.ExternalEmissivity, extEmissivity);
            result.SetValue(OpaqueMaterialParameter.InternalEmissivity, intEmissivity);
            result.SetValue(global::SAM.Analytical.MaterialParameter.VapourDiffusionFactor, vapour);
            return result;
        }

        private static TransparentMaterial Glass(string name, double conductivity, double solarT, double lightT, double extSolarR, double intSolarR, double extLightR, double intLightR, double extE, double intE)
        {
            TransparentMaterial result = new TransparentMaterial(name, "S08 PR-C fixture", name, name, conductivity, 840, 2500);
            result.SetValue(TransparentMaterialParameter.SolarTransmittance, solarT);
            result.SetValue(TransparentMaterialParameter.LightTransmittance, lightT);
            result.SetValue(TransparentMaterialParameter.ExternalSolarReflectance, extSolarR);
            result.SetValue(TransparentMaterialParameter.InternalSolarReflectance, intSolarR);
            result.SetValue(TransparentMaterialParameter.ExternalLightReflectance, extLightR);
            result.SetValue(TransparentMaterialParameter.InternalLightReflectance, intLightR);
            result.SetValue(TransparentMaterialParameter.ExternalEmissivity, extE);
            result.SetValue(TransparentMaterialParameter.InternalEmissivity, intE);
            result.SetValue(global::SAM.Analytical.MaterialParameter.VapourDiffusionFactor, 99999);
            result.SetValue(TransparentMaterialParameter.IsBlind, false);
            return result;
        }

        private static MaterialLibrary Materials()
        {
            MaterialLibrary result = new MaterialLibrary("S08 PR-C fixture materials");
            result.Add(Opaque("FIX_Plasterboard", 0.21, 1090, 700, 0.62, 0.68, 0.64, 0.70, 0.90, 0.91, 8));
            result.Add(Opaque("FIX_MineralWool", 0.037, 1030, 45, 0.48, 0.51, 0.49, 0.53, 0.89, 0.92, 1.2));
            result.Add(Opaque("FIX_Blockwork", 0.19, 1000, 1400, 0.38, 0.44, 0.39, 0.46, 0.93, 0.94, 10));
            result.Add(Opaque("FIX_Render", 0.87, 920, 1750, 0.57, 0.36, 0.58, 0.37, 0.88, 0.93, 25));
            result.Add(Opaque("FIX_PIR", 0.022, 1400, 32, 0.53, 0.55, 0.54, 0.56, 0.87, 0.95, 60));
            result.Add(Opaque("FIX_OSB", 0.13, 1700, 600, 0.43, 0.47, 0.42, 0.48, 0.91, 0.89, 165));
            result.Add(Opaque("FIX_Membrane", 0.17, 1460, 1180, 0.14, 0.24, 0.15, 0.25, 0.92, 0.86, 50000));
            result.Add(Opaque("FIX_Screed", 1.28, 840, 2050, 0.27, 0.33, 0.28, 0.34, 0.94, 0.90, 15));
            result.Add(Opaque("FIX_EPS", 0.034, 1450, 22, 0.58, 0.61, 0.59, 0.63, 0.85, 0.96, 40));
            result.Add(Opaque("FIX_Concrete", 1.95, 880, 2300, 0.31, 0.29, 0.32, 0.30, 0.95, 0.87, 110));
            result.Add(Opaque("FIX_Timber", 0.14, 1600, 520, 0.41, 0.39, 0.40, 0.38, 0.86, 0.88, 50));
            result.Add(Opaque("FIX_Stud", 0.12, 1880, 480, 0.66, 0.64, 0.67, 0.65, 0.84, 0.83, 45));
            result.Add(Glass("FIX_LaminatedGlass", 0.96, 0.746, 0.861, 0.069, 0.067, 0.079, 0.077, 0.840, 0.837));
            return result;
        }

        private static Construction Construction(string guid, string name, params ConstructionLayer[] layers)
        {
            return new Construction(System.Guid.Parse(guid), name, layers);
        }

        private static ConstructionLayer L(string material, double thickness)
        {
            return new ConstructionLayer(material, thickness);
        }

        public static Construction ExternalWall { get; } = Construction("5080c000-0000-0000-0000-0000000000a1", "FIX_EXT_WALL", L("FIX_Plasterboard", 0.0125), L("FIX_MineralWool", 0.14), L("FIX_Blockwork", 0.1), L("FIX_Render", 0.015));
        public static Construction Roof { get; } = Construction("5080c000-0000-0000-0000-0000000000a2", "FIX_ROOF", L("FIX_Plasterboard", 0.015), L("FIX_PIR", 0.16), L("FIX_OSB", 0.018), L("FIX_Membrane", 0.004));
        public static Construction GroundFloor { get; } = Construction("5080c000-0000-0000-0000-0000000000a3", "FIX_GRD_FLOOR", L("FIX_Screed", 0.065), L("FIX_EPS", 0.12), L("FIX_Concrete", 0.15));
        public static Construction Partition { get; } = Construction("5080c000-0000-0000-0000-0000000000a4", "FIX_PARTITION", L("FIX_Plasterboard", 0.0125), L("FIX_MineralWool", 0.05), L("FIX_Stud", 0.022), L("FIX_Timber", 0.015));

        public static ApertureConstruction FramedWindow { get; } = Glazing("5080c000-0000-0000-0000-0000000000b1", "FIX_WIN_FRAMED", 0.0088, 0.075, 0.065);
        public static ApertureConstruction UnframedWindow { get; } = Glazing("5080c000-0000-0000-0000-0000000000b2", "FIX_WIN_UNFRAMED", 0.0088, double.NaN, double.NaN);

        private static ApertureConstruction Glazing(string guid, string name, double pane, double frame, double defaultFrameWidth)
        {
            bool framed = !double.IsNaN(frame);
            ApertureConstruction result = new ApertureConstruction(System.Guid.Parse(guid), name, ApertureType.Window, new[] { L("FIX_LaminatedGlass", pane) }, framed ? new[] { L("FIX_Timber", frame) } : null);
            result.SetValue(ApertureConstructionParameter.Transparent, true);
            if (framed)
            {
                result.SetValue(ApertureConstructionParameter.DefaultFrameWidth, defaultFrameWidth);
            }

            return result;
        }

        // ---- geometry ---------------------------------------------------------------------------------------------------

        private static Point3D P(double x, double y, double z)
        {
            return new Point3D(x, y, z);
        }

        private static Face3D Quad(Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            return new Face3D(new Polygon3D(new[] { p0, p1, p2, p3 }));
        }

        // Every panel is a centre-line face carrying the physical thickness of its source element.
        private static Panel Panel(Construction construction, PanelType panelType, Face3D face3D)
        {
            Panel result = Analytical.Create.Panel(construction, panelType, face3D);
            result.SetValue(PanelSourceParameter.PhysicalThickness, construction.ConstructionLayers.Sum(x => x.Thickness));
            return result;
        }

        /// <summary>Floor, roof, south (y0), east (x1), north (y1), west (x0) of an axis-aligned centre-line box, outward wound.</summary>
        private static List<Panel> Shell(double x0, double x1, double y0, double y1, double z0, double z1)
        {
            return new List<Panel>
            {
                Panel(GroundFloor, PanelType.SlabOnGrade, Quad(P(x0, y0, z0), P(x0, y1, z0), P(x1, y1, z0), P(x1, y0, z0))),
                Panel(Roof, PanelType.Roof, Quad(P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1))),
                Panel(ExternalWall, PanelType.WallExternal, Quad(P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1))),
                Panel(ExternalWall, PanelType.WallExternal, Quad(P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1))),
                Panel(ExternalWall, PanelType.WallExternal, Quad(P(x1, y1, z0), P(x0, y1, z0), P(x0, y1, z1), P(x1, y1, z1))),
                Panel(ExternalWall, PanelType.WallExternal, Quad(P(x0, y1, z0), P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1)))
            };
        }

        // ---- schedules: plausible office/residential shapes, distinct per slot -----------------------------------------

        private static Profile Profile(ProfileLibrary library, string name, ProfileType type, IEnumerable<double> values)
        {
            Profile result = new Profile(name, type, values);
            library.Add(result);
            return result;
        }

        // A working day: off at night, ramps, a lunch dip; never two slots of one zone the same shape.
        private static double[] Day(double night, double morning, double day, double lunch, double evening, int start, int end)
        {
            double[] result = new double[24];
            for (int h = 0; h < 24; h++)
            {
                result[h] = h < start || h >= end ? night : h == start ? morning : h == 12 ? lunch : h == end - 1 ? evening : day;
            }

            return result;
        }

        // 8760 hours from a weekday and a weekend day; day 0 is a Monday of the fixture's own convention.
        private static double[] Year(double[] weekday, double[] weekend)
        {
            double[] result = new double[8760];
            for (int d = 0; d < 365; d++)
            {
                double[] day = d % 7 >= 5 ? weekend : weekday;
                for (int h = 0; h < 24; h++)
                {
                    result[d * 24 + h] = day[h];
                }
            }

            return result;
        }

        private static double[] Setpoint(double setback, double occupied, int start, int end)
        {
            return Enumerable.Range(0, 24).Select(h => h >= start && h < end ? occupied : setback).ToArray();
        }

        // ---- internal conditions ----------------------------------------------------------------------------------------

        private sealed class Condition
        {
            public string Name;
            public double Occupancy, SensiblePerPerson, LatentPerPerson, LightingPerArea, EquipmentSensiblePerArea, EquipmentLatentPerArea, InfiltrationAch;
            public double LightingRadiant, OccupancyRadiant, EquipmentRadiant;
            public double SupplyPerPerson = double.NaN, SupplyPerArea = double.NaN, SupplyAch = double.NaN;
            public double[] OccupancyShape, LightingShape, EquipmentSensibleShape, EquipmentLatentShape, InfiltrationShape, HeatingShape, CoolingShape, HumidificationShape, DehumidificationShape, VentilationShape;
        }

        private static void Apply(Space space, ProfileLibrary library, Condition c)
        {
            InternalCondition ic = new InternalCondition(c.Name);
            ic.SetValue(InternalConditionParameter.OccupancySensibleGainPerPerson, c.SensiblePerPerson);
            ic.SetValue(InternalConditionParameter.OccupancyLatentGainPerPerson, c.LatentPerPerson);
            ic.SetValue(InternalConditionParameter.LightingGainPerArea, c.LightingPerArea);
            ic.SetValue(InternalConditionParameter.EquipmentSensibleGainPerArea, c.EquipmentSensiblePerArea);
            ic.SetValue(InternalConditionParameter.EquipmentLatentGainPerArea, c.EquipmentLatentPerArea);
            ic.SetValue(InternalConditionParameter.InfiltrationAirChangesPerHour, c.InfiltrationAch);
            ic.SetValue(InternalConditionParameter.LightingRadiantProportion, c.LightingRadiant);
            ic.SetValue(InternalConditionParameter.OccupancyRadiantProportion, c.OccupancyRadiant);
            ic.SetValue(InternalConditionParameter.EquipmentRadiantProportion, c.EquipmentRadiant);
            if (!double.IsNaN(c.SupplyPerPerson)) ic.SetValue(InternalConditionParameter.SupplyAirFlowPerPerson, c.SupplyPerPerson);
            if (!double.IsNaN(c.SupplyPerArea)) ic.SetValue(InternalConditionParameter.SupplyAirFlowPerArea, c.SupplyPerArea);
            if (!double.IsNaN(c.SupplyAch)) ic.SetValue(InternalConditionParameter.SupplyAirChangesPerHour, c.SupplyAch);

            string p = c.Name + "_";
            ic.SetValue(InternalConditionParameter.OccupancyProfileName, Profile(library, p + "OCC", ProfileType.Occupancy, c.OccupancyShape).Name);
            ic.SetValue(InternalConditionParameter.LightingProfileName, Profile(library, p + "LGT", ProfileType.Lighting, c.LightingShape).Name);
            ic.SetValue(InternalConditionParameter.EquipmentSensibleProfileName, Profile(library, p + "EQS", ProfileType.EquipmentSensible, c.EquipmentSensibleShape).Name);
            ic.SetValue(InternalConditionParameter.EquipmentLatentProfileName, Profile(library, p + "EQL", ProfileType.EquipmentLatent, c.EquipmentLatentShape).Name);
            ic.SetValue(InternalConditionParameter.InfiltrationProfileName, Profile(library, p + "INF", ProfileType.Infiltration, c.InfiltrationShape).Name);
            ic.SetValue(InternalConditionParameter.HeatingProfileName, Profile(library, p + "HTG", ProfileType.Heating, c.HeatingShape).Name);
            ic.SetValue(InternalConditionParameter.CoolingProfileName, Profile(library, p + "CLG", ProfileType.Cooling, c.CoolingShape).Name);
            ic.SetValue(InternalConditionParameter.HumidificationProfileName, Profile(library, p + "HUM", ProfileType.Humidification, c.HumidificationShape).Name);
            ic.SetValue(InternalConditionParameter.DehumidificationProfileName, Profile(library, p + "DHU", ProfileType.Dehumidification, c.DehumidificationShape).Name);
            if (c.VentilationShape != null)
            {
                ic.SetValue(InternalConditionParameter.VentilationProfileName, Profile(library, p + "VNT", ProfileType.Ventilation, c.VentilationShape).Name);
            }

            space.SetValue(global::SAM.Analytical.SpaceParameter.Occupancy, c.Occupancy);
            space.InternalCondition = ic;
        }

        private static void Stored(Space space, double area, double volume)
        {
            space.SetValue(global::SAM.Analytical.SpaceParameter.Area, area);
            space.SetValue(global::SAM.Analytical.SpaceParameter.Volume, volume);
        }

        private static AnalyticalModel Model(string name, string description, AdjacencyCluster adjacencyCluster, ProfileLibrary profileLibrary)
        {
            return new AnalyticalModel(name, description, null, null, adjacencyCluster, Materials(), profileLibrary);
        }

        private static AdjacencyCluster Cluster(params KeyValuePair<Space, List<Panel>>[] spaces)
        {
            AdjacencyCluster result = new AdjacencyCluster();
            foreach (KeyValuePair<Space, List<Panel>> pair in spaces)
            {
                result.AddObject(pair.Key);
            }

            foreach (KeyValuePair<Space, List<Panel>> pair in spaces)
            {
                foreach (Panel panel in pair.Value)
                {
                    result.AddObject(panel);
                    result.AddRelation(pair.Key, panel);
                }
            }

            return result;
        }

        // ---- the fixtures -----------------------------------------------------------------------------------------------

        public const double BoxX = 6.4, BoxY = 4.2, BoxZ = 2.85;

        /// <summary>
        /// The box: one 6.4 x 4.2 x 2.85 m centre-line zone, a framed 2.4 x 1.35 m south window and an unframed 0.9 x 1.2 m
        /// west window, every S07 gain, setpoint and humidistat, infiltration, and <b>V2</b> ventilation: a per-person
        /// requirement with an explicitly selected Ventilation profile (Building Simulator delivery).
        /// </summary>
        public static AnalyticalModel CreateBox()
        {
            ProfileLibrary library = new ProfileLibrary("S08 PR-C fixture profiles");
            Space space = new Space(System.Guid.Parse("5080c000-0000-0000-0000-00000000c001"), "FixBox", P(BoxX / 2, BoxY / 2, 1.2));
            Stored(space, BoxX * BoxY, BoxX * BoxY * BoxZ);

            List<Panel> panels = Shell(0, BoxX, 0, BoxY, 0, BoxZ);
            panels[2].AddAperture(Analytical.Create.Aperture(FramedWindow, Quad(P(1.6, 0, 0.85), P(4.0, 0, 0.85), P(4.0, 0, 2.2), P(1.6, 0, 2.2))));
            panels[5].AddAperture(Analytical.Create.Aperture(UnframedWindow, Quad(P(0, 2.9, 0.9), P(0, 2.0, 0.9), P(0, 2.0, 2.1), P(0, 2.9, 2.1))));

            Apply(space, library, new Condition
            {
                Name = "FIX_IC_BOX", Occupancy = 3.0, SensiblePerPerson = 75.0, LatentPerPerson = 55.0, LightingPerArea = 7.4,
                EquipmentSensiblePerArea = 12.6, EquipmentLatentPerArea = 0.8, InfiltrationAch = 0.3,
                LightingRadiant = 0.37, OccupancyRadiant = 0.21, EquipmentRadiant = 0.24,
                SupplyPerPerson = 0.01,
                OccupancyShape = Day(0, 0.35, 0.9, 0.55, 0.4, 8, 18),
                LightingShape = Day(0.05, 0.6, 0.85, 0.75, 0.5, 7, 19),
                EquipmentSensibleShape = Year(Day(0.15, 0.55, 0.95, 0.7, 0.45, 8, 18), Day(0.12, 0.12, 0.2, 0.18, 0.12, 9, 13)),
                EquipmentLatentShape = new[] { 0.4 },
                InfiltrationShape = Day(1.0, 0.8, 0.65, 0.7, 0.85, 7, 19),
                HeatingShape = Setpoint(16.0, 21.0, 7, 19),
                CoolingShape = Setpoint(28.0, 24.0, 7, 19),
                HumidificationShape = new[] { 35.0 },
                DehumidificationShape = new[] { 65.0 },
                VentilationShape = Day(0, 1.0, 1.0, 0.8, 0.6, 7, 19)
            });

            return Model(Box, "S08 PR-C equivalence fixture: box, V2, " + PanelConvention, Cluster(new KeyValuePair<Space, List<Panel>>(space, panels)), library);
        }

        public const double TwoXA = 4.5, TwoXB = 8.1, TwoY = 5.4, TwoZ = 3.05;

        /// <summary>
        /// The multizone model: two centre-line zones (A: x 0..4.5, B: x 4.5..8.1; y 0..5.4; 3.05 m) sharing one asymmetric
        /// partition, distinct conditions, both <b>V1</b>: a ventilation requirement (A per person and per area, B air changes)
        /// with no Ventilation profile, so no delivery is selected. A has a framed south window, B a framed north and an
        /// unframed east window.
        /// </summary>
        public static AnalyticalModel CreateTwoZones()
        {
            ProfileLibrary library = new ProfileLibrary("S08 PR-C fixture profiles");
            Space a = new Space(System.Guid.Parse("5080c000-0000-0000-0000-00000000c0a1"), "FixA", P(TwoXA / 2, TwoY / 2, 1.2));
            Space b = new Space(System.Guid.Parse("5080c000-0000-0000-0000-00000000c0b1"), "FixB", P((TwoXA + TwoXB) / 2, TwoY / 2, 1.2));
            Stored(a, TwoXA * TwoY, TwoXA * TwoY * TwoZ);
            Stored(b, (TwoXB - TwoXA) * TwoY, (TwoXB - TwoXA) * TwoY * TwoZ);

            List<Panel> panels_A = Shell(0, TwoXA, 0, TwoY, 0, TwoZ);
            List<Panel> panels_B = Shell(TwoXA, TwoXB, 0, TwoY, 0, TwoZ);
            panels_A.RemoveAt(3); // east of A: the partition
            panels_B.RemoveAt(5); // west of B: the partition
            Panel partition = Panel(Partition, PanelType.WallInternal, Quad(P(TwoXA, 0, 0), P(TwoXA, TwoY, 0), P(TwoXA, TwoY, TwoZ), P(TwoXA, 0, TwoZ)));

            panels_A[2].AddAperture(Analytical.Create.Aperture(FramedWindow, Quad(P(0.8, 0, 0.95), P(3.3, 0, 0.95), P(3.3, 0, 2.35), P(0.8, 0, 2.35))));
            panels_B[3].AddAperture(Analytical.Create.Aperture(FramedWindow, Quad(P(7.4, TwoY, 1.0), P(5.2, TwoY, 1.0), P(5.2, TwoY, 2.2), P(7.4, TwoY, 2.2))));
            panels_B[2].AddAperture(Analytical.Create.Aperture(UnframedWindow, Quad(P(TwoXB, 1.5, 1.1), P(TwoXB, 2.6, 1.1), P(TwoXB, 2.6, 2.0), P(TwoXB, 1.5, 2.0))));

            Apply(a, library, new Condition
            {
                Name = "FIX_IC_A", Occupancy = 2.0, SensiblePerPerson = 70.0, LatentPerPerson = 45.0, LightingPerArea = 6.2,
                EquipmentSensiblePerArea = 9.4, EquipmentLatentPerArea = 0.6, InfiltrationAch = 0.22,
                LightingRadiant = 0.34, OccupancyRadiant = 0.26, EquipmentRadiant = 0.19,
                SupplyPerPerson = 0.008, SupplyPerArea = 0.0006,
                OccupancyShape = Day(0.1, 0.4, 0.85, 0.6, 0.5, 8, 17),
                LightingShape = Day(0, 0.5, 0.8, 0.7, 0.45, 7, 18),
                EquipmentSensibleShape = Day(0.2, 0.5, 0.9, 0.65, 0.35, 8, 17),
                EquipmentLatentShape = new[] { 0.3 },
                InfiltrationShape = new[] { 1.0 },
                HeatingShape = Setpoint(15.0, 20.0, 7, 18),
                CoolingShape = Setpoint(29.0, 25.0, 7, 18),
                HumidificationShape = new[] { 30.0 },
                DehumidificationShape = new[] { 70.0 }
            });

            Apply(b, library, new Condition
            {
                Name = "FIX_IC_B", Occupancy = 1.0, SensiblePerPerson = 65.0, LatentPerPerson = 40.0, LightingPerArea = 4.8,
                EquipmentSensiblePerArea = 5.5, EquipmentLatentPerArea = 0.4, InfiltrationAch = 0.4,
                LightingRadiant = 0.41, OccupancyRadiant = 0.18, EquipmentRadiant = 0.28,
                SupplyAch = 0.75,
                OccupancyShape = Day(0.9, 0.6, 0.2, 0.3, 0.7, 9, 18),
                LightingShape = Day(0.1, 0.65, 0.3, 0.35, 0.9, 6, 23),
                EquipmentSensibleShape = Year(Day(0.25, 0.4, 0.3, 0.5, 0.6, 6, 22), Day(0.3, 0.5, 0.55, 0.65, 0.7, 7, 23)),
                EquipmentLatentShape = new[] { 0.5 },
                InfiltrationShape = Day(0.9, 1.0, 1.0, 1.0, 0.95, 6, 22),
                HeatingShape = Setpoint(17.0, 19.5, 6, 23),
                CoolingShape = Setpoint(27.0, 26.0, 6, 23),
                HumidificationShape = new[] { 40.0 },
                DehumidificationShape = new[] { 60.0 }
            });

            AdjacencyCluster cluster = Cluster(new KeyValuePair<Space, List<Panel>>(a, panels_A), new KeyValuePair<Space, List<Panel>>(b, panels_B));
            cluster.AddObject(partition);
            cluster.AddRelation(a, partition);
            cluster.AddRelation(b, partition);

            return Model(TwoZones, "S08 PR-C equivalence fixture: two zones, V1, " + PanelConvention, cluster, library);
        }
    }
}
