// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;

// Compiled into BOTH the licensed validation harness and the COM-free unit tests (SAM.Analytical.Tas.TM59.Tests links it).
// It touches no TAS type.
namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// A small three-zone building built so that the two SAM -> T3D routes cannot agree by accident: every property that decides a
    /// load differs from zone to zone, so a mix-up of identities, sides, orientations or openings changes the answer instead of
    /// cancelling.
    /// <code>
    ///   y=8  +---------------------+
    ///        |  C  Store  (h = 4)  |   north window, west wall, east wall ADIABATIC, south strip above A
    ///   y=5  +---------------------+--------------+
    ///        |  A  Office (h = 3)  |  B  Meeting  |   A: south 4.0 x 1.5 window, west 1.5 x 1.2 window
    ///        |   8 x 5 = 40 m2     |  4 x 5 = 20  |   B: south 0.8 x 1.0, east 2.0 x 1.5; north wall opaque
    ///   y=0  +---------------------+--------------+
    ///       x=0                   x=8           x=12
    /// </code>
    /// <list type="bullet">
    /// <item>Three zone sizes (120, 60 and 96 m3; C is taller than its neighbours, so C's south face is a partition to A plus an
    /// external strip above A's roof).</item>
    /// <item>External walls facing south (A, B, C-strip), west (A, C), east (B, C - adiabatic) and north (B, C); windows on every
    /// orientation, five of them in five different sizes, in two glazing types with different solar transmittance.</item>
    /// <item>Three external wall, three roof and two ground floor constructions; every layer a distinct material.</item>
    /// <item>A-B: an <b>asymmetric</b> partition (insulation on the A side, plaster on the B side), stored with its relations
    /// B first although A is first in the model - the case that decides which zone surface TAS must reverse. A-C: a symmetric
    /// partition.</item>
    /// <item>One adiabatic external wall (C east).</item>
    /// <item>Internal conditions that differ in every field the workflow writes: occupancy density and profile, equipment and
    /// lighting gains and profiles, infiltration rate, supply ventilation rate and profile, and heating and cooling set-point
    /// profiles (C is never cooled, B is cooled hardest).</item>
    /// </list>
    /// </summary>
    /// <summary>The order the fixture's panels are added to the model - see <see cref="LoadSensitiveModel.Create"/>.</summary>
    public enum PanelOrder
    {
        AsListed,
        Reversed,
        Rotated
    }

    public static class LoadSensitiveModel
    {
        public const string SpaceName_Office = "Office_A";
        public const string SpaceName_Meeting = "Meeting_B";
        public const string SpaceName_Store = "Store_C";

        private static Point3D P(double x, double y, double z)
        {
            return new Point3D(x, y, z);
        }

        private static Face3D Quad(Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            return SyntheticModels.Quad(p0, p1, p2, p3);
        }

        private static Construction Construction(string name, string guidSuffix, params ConstructionLayer[] layers)
        {
            return new Construction(System.Guid.Parse("00000000-0000-0000-0000-" + guidSuffix.PadLeft(12, '0')), name, layers);
        }

        private static ConstructionLayer L(string material, double thickness)
        {
            return new ConstructionLayer(material, thickness);
        }

        // Outside -> inside.
        public static Construction ExtWallA { get; } = Construction("EXT_WALL_A", "c01", L("Brick", 0.102), L("Insulation", 0.100), L("Block", 0.100), L("Plaster", 0.013));
        public static Construction ExtWallB { get; } = Construction("EXT_WALL_B", "c02", L("Timber", 0.020), L("Insulation", 0.150), L("Plasterboard", 0.0125));
        public static Construction ExtWallC { get; } = Construction("EXT_WALL_C", "c03", L("Concrete", 0.200));
        public static Construction RoofA { get; } = Construction("ROOF_A", "c04", L("Insulation", 0.120), L("Concrete", 0.150), L("Plaster", 0.013));
        public static Construction RoofB { get; } = Construction("ROOF_B", "c05", L("Metal deck", 0.002), L("Insulation", 0.080), L("Plasterboard", 0.0125));
        public static Construction RoofC { get; } = Construction("ROOF_C", "c06", L("Timber", 0.025), L("Insulation", 0.060), L("Plasterboard", 0.0125));
        public static Construction FloorA { get; } = Construction("GRD_FLOOR_A", "c07", L("Concrete", 0.200), L("Insulation", 0.080));
        public static Construction FloorB { get; } = Construction("GRD_FLOOR_B", "c08", L("Concrete", 0.250), L("Insulation", 0.040));

        /// <summary>The asymmetric partition: listed from the A side, insulation then concrete then plaster.</summary>
        public static Construction PartitionAB { get; } = Construction("PART_AB_ASYM", "c09", L("Insulation", 0.075), L("Concrete", 0.150), L("Plaster", 0.013));
        public static Construction PartitionAC { get; } = Construction("PART_AC_SYM", "c10", L("Plasterboard", 0.0125), L("Insulation", 0.075), L("Plasterboard", 0.0125));

        public static ApertureConstruction GlazingDouble { get; } = Glazing("GLZ_DOUBLE", "d01", "Low-e double", 0.024, 0.08);
        public static ApertureConstruction GlazingSingle { get; } = Glazing("GLZ_SINGLE", "d02", "Clear single", 0.006, 0.05);

        private static ApertureConstruction Glazing(string name, string guidSuffix, string paneMaterial, double paneThickness, double frameWidth)
        {
            ApertureConstruction result = new ApertureConstruction(System.Guid.Parse("00000000-0000-0000-0000-" + guidSuffix.PadLeft(12, '0')), name, ApertureType.Window, new[] { new ConstructionLayer(paneMaterial, paneThickness) }, new[] { new ConstructionLayer("Frame", 0.07) });
            result.SetValue(ApertureConstructionParameter.Transparent, true);
            result.SetValue(ApertureConstructionParameter.DefaultFrameWidth, frameWidth);
            return result;
        }

        // Fully specified, as a real material library entry is: TAS refuses a construction whose material has no default thickness
        // ("Building element has an illegal construction assigned to it"), and the solar reflectances and emissivities are inputs
        // to the simulation, so they differ from material to material.
        private static SAM.Core.OpaqueMaterial Opaque(string name, double conductivity, double specificHeat, double density, double externalSolarReflectance, double internalSolarReflectance, double emissivity = 0.9)
        {
            return Analytical.Create.OpaqueMaterial(name, "LoadSensitive", name, name, conductivity, specificHeat, density, 0.1, 10, externalSolarReflectance, internalSolarReflectance, externalSolarReflectance, internalSolarReflectance, emissivity, emissivity, false);
        }

        public static SAM.Core.MaterialLibrary Materials()
        {
            SAM.Core.MaterialLibrary result = new SAM.Core.MaterialLibrary("LoadSensitive materials");
            result.Add(Opaque("Brick", 0.77, 800, 1700, 0.30, 0.30));
            result.Add(Opaque("Insulation", 0.035, 1000, 30, 0.40, 0.40));
            result.Add(Opaque("Block", 0.51, 1000, 1400, 0.35, 0.35));
            result.Add(Opaque("Plaster", 0.40, 840, 1000, 0.50, 0.55));
            result.Add(Opaque("Plasterboard", 0.25, 1000, 900, 0.60, 0.65));
            result.Add(Opaque("Timber", 0.13, 1600, 500, 0.35, 0.40));
            result.Add(Opaque("Concrete", 1.40, 1000, 2100, 0.45, 0.45));
            result.Add(Opaque("Metal deck", 50.0, 450, 7800, 0.70, 0.60, 0.25));
            result.Add(Opaque("Frame", 0.13, 1600, 500, 0.30, 0.30));

            // Two glazings that differ in everything the simulation reads: conductivity, solar and light transmittance, reflectance.
            result.Add(Analytical.Create.TransparentMaterial("Low-e double", "LoadSensitive", "Low-e double", "Low-e double", 0.12, 0.024, double.NaN, 0.42, 0.70, 0.30, 0.28, 0.12, 0.12, 0.84, 0.10, false));
            result.Add(Analytical.Create.TransparentMaterial("Clear single", "LoadSensitive", "Clear single", "Clear single", 1.0, 0.006, double.NaN, 0.78, 0.88, 0.07, 0.07, 0.08, 0.08, 0.84, 0.84, false));
            return result;
        }

        private static double[] Hours(double night, double day, int from, int to, double shoulder = double.NaN)
        {
            double[] result = new double[24];
            for (int i = 0; i < 24; i++)
            {
                result[i] = i >= from && i < to ? day : night;
            }

            if (!double.IsNaN(shoulder))
            {
                result[from - 1] = shoulder;
                result[to] = shoulder;
            }

            return result;
        }

        private static Profile Profile(string name, ProfileType profileType, double[] hours)
        {
            return new Profile(name, profileType, hours);
        }

        public static ProfileLibrary Profiles()
        {
            ProfileLibrary result = new ProfileLibrary("LoadSensitive profiles");

            result.Add(Profile("Office occupancy", ProfileType.Occupancy, Hours(0, 1, 9, 17, 0.5)));
            result.Add(Profile("Meeting occupancy", ProfileType.Occupancy, Hours(0, 1, 10, 16, 0.2)));
            result.Add(Profile("Store occupancy", ProfileType.Occupancy, Hours(0, 1, 11, 13)));

            result.Add(Profile("Office equipment", ProfileType.EquipmentSensible, Hours(0.2, 1, 9, 17, 0.6)));
            result.Add(Profile("Meeting equipment", ProfileType.EquipmentSensible, Hours(0.1, 1, 10, 16)));
            result.Add(Profile("Store equipment", ProfileType.EquipmentSensible, Hours(0.5, 0.5, 0, 24)));

            result.Add(Profile("Office lighting", ProfileType.Lighting, Hours(0.05, 1, 8, 18)));
            result.Add(Profile("Meeting lighting", ProfileType.Lighting, Hours(0, 1, 10, 16)));
            result.Add(Profile("Store lighting", ProfileType.Lighting, Hours(0, 1, 11, 13)));

            result.Add(Profile("Office infiltration", ProfileType.Infiltration, Hours(1, 1, 0, 24)));
            result.Add(Profile("Meeting infiltration", ProfileType.Infiltration, Hours(1, 1, 0, 24)));
            result.Add(Profile("Store infiltration", ProfileType.Infiltration, Hours(1, 1, 0, 24)));

            result.Add(Profile("Office ventilation", ProfileType.Ventilation, Hours(0, 1, 8, 18)));
            result.Add(Profile("Meeting ventilation", ProfileType.Ventilation, Hours(0, 1, 9, 17)));

            result.Add(Profile("Office heating", ProfileType.Heating, Hours(16, 21, 7, 18)));
            result.Add(Profile("Meeting heating", ProfileType.Heating, Hours(12, 20, 9, 17)));
            result.Add(Profile("Store heating", ProfileType.Heating, Hours(12, 12, 0, 24)));

            result.Add(Profile("Office cooling", ProfileType.Cooling, Hours(28, 24, 7, 18)));
            result.Add(Profile("Meeting cooling", ProfileType.Cooling, Hours(30, 23, 9, 17)));
            result.Add(Profile("Store cooling", ProfileType.Cooling, Hours(35, 35, 0, 24)));

            return result;
        }

        private static InternalCondition InternalCondition(string name, string prefix, double areaPerPerson, double sensiblePerPerson, double latentPerPerson, double equipmentPerArea, double lightingPerArea, double infiltrationAch, double supplyAch, bool ventilation)
        {
            InternalCondition result = Analytical.Create.InternalCondition(name);

            result.SetValue(InternalConditionParameter.AreaPerPerson, areaPerPerson);
            result.SetValue(InternalConditionParameter.OccupancySensibleGainPerPerson, sensiblePerPerson);
            result.SetValue(InternalConditionParameter.OccupancyLatentGainPerPerson, latentPerPerson);
            result.SetValue(InternalConditionParameter.EquipmentSensibleGainPerArea, equipmentPerArea);
            result.SetValue(InternalConditionParameter.LightingGainPerArea, lightingPerArea);
            result.SetValue(InternalConditionParameter.InfiltrationAirChangesPerHour, infiltrationAch);

            result.SetProfileName(ProfileType.Occupancy, prefix + " occupancy");
            result.SetProfileName(ProfileType.EquipmentSensible, prefix + " equipment");
            result.SetProfileName(ProfileType.Lighting, prefix + " lighting");
            result.SetProfileName(ProfileType.Infiltration, prefix + " infiltration");
            result.SetProfileName(ProfileType.Heating, prefix + " heating");
            result.SetProfileName(ProfileType.Cooling, prefix + " cooling");

            if (ventilation)
            {
                result.SetValue(InternalConditionParameter.SupplyAirChangesPerHour, supplyAch);
                result.SetProfileName(ProfileType.Ventilation, prefix + " ventilation");
            }

            return result;
        }

        // Every object gets a fixed GUID, in creation order. The two routes run in separate processes and the comparison matches objects by
        // GUID (apertures) and by SAM space guid, so a model that minted fresh ones on every call could not be compared with itself.
        private sealed class Ids
        {
            private int space = 0, panel = 100, aperture = 200;

            private static System.Guid Guid(int n)
            {
                return System.Guid.Parse("00000000-0000-0000-0001-" + n.ToString("D12"));
            }

            public System.Guid Space() { return Guid(++space); }
            public System.Guid Panel() { return Guid(++panel); }
            public System.Guid Aperture() { return Guid(++aperture); }
        }

        private static Panel Wall(Ids ids, Construction construction, PanelType panelType, Face3D face3D, bool adiabatic = false)
        {
            return Analytical.Create.Panel(ids.Panel(), SyntheticModels.Panel(construction, panelType, face3D, adiabatic: adiabatic));
        }

        private static void Window(Ids ids, Panel panel, ApertureConstruction apertureConstruction, Point3D p0, Point3D p1, Point3D p2, Point3D p3, IOpeningProperties openingProperties = null)
        {
            Aperture aperture = new Aperture(ids.Aperture(), Analytical.Create.Aperture(apertureConstruction, Quad(p0, p1, p2, p3)));
            if (openingProperties != null)
            {
                aperture.SetValue(Analytical.ApertureParameter.OpeningProperties, openingProperties);
            }

            panel.AddAperture(aperture);
        }

        /// <summary>
        /// The model: three spaces, 17 panels (A 4, B 5, C 6 and the two shared partitions) and five windows, two of them openable
        /// (natural ventilation: Office south unrestricted at Cd 0.62, Meeting east closed at night at Cd 0.70, factor 0.6).
        /// </summary>
        /// <param name="reverseModelOrder">
        /// List the spaces C, B, A instead of A, B, C. Nothing else changes, but SAM's convention for which side of a partition is
        /// "reversed" follows the model order, so every partition's reversed side moves to the other zone - which is exactly the case in
        /// which TAS's own geometric choice and the direct route's <c>UpdateReversed</c> repair disagree.
        /// </param>
        /// <param name="panelOrder">
        /// The order the panels are added to the model. The model is the same building - same spaces, same panels, same relations - but TAS
        /// creates its surfaces in a different order, which is the one thing the two routes also do differently. Run through ONE route, the
        /// difference it makes to the simulation is that route's own order sensitivity: the yardstick for any difference BETWEEN the routes.
        /// </param>
        public static AnalyticalModel Create(bool reverseModelOrder = false, PanelOrder panelOrder = PanelOrder.AsListed)
        {
            Ids ids = new Ids();
            Space officeA = new Space(ids.Space(), SpaceName_Office, P(4, 2.5, 1.5));
            Space meetingB = new Space(ids.Space(), SpaceName_Meeting, P(10, 2.5, 1.5));
            Space storeC = new Space(ids.Space(), SpaceName_Store, P(4, 6.5, 2));

            officeA.InternalCondition = InternalCondition(SpaceName_Office, "Office", 10, 70, 45, 15, 12, 0.4, 2.0, true);
            meetingB.InternalCondition = InternalCondition(SpaceName_Meeting, "Meeting", 3, 80, 55, 6, 9, 0.8, 4.0, true);
            storeC.InternalCondition = InternalCondition(SpaceName_Store, "Store", 100, 60, 35, 2, 4, 1.5, 0, false);

            // What a real space carries from its authoring tool: the level, and the area and volume the gain calculations multiply
            // by (a space without an Area has every per-area gain calculated as zero).
            foreach ((Space space, double area, double volume) in new[] { (officeA, Expected.FloorAreaA, Expected.VolumeA), (meetingB, Expected.FloorAreaB, Expected.VolumeB), (storeC, Expected.FloorAreaC, Expected.VolumeC) })
            {
                space.SetValue(Analytical.SpaceParameter.LevelName, "Level 0");
                space.SetValue(Analytical.SpaceParameter.Area, area);
                space.SetValue(Analytical.SpaceParameter.Volume, volume);
            }

            // ---- A: office, x 0..8, y 0..5, z 0..3
            Panel floorA = Wall(ids, FloorA, PanelType.SlabOnGrade, Quad(P(0, 0, 0), P(0, 5, 0), P(8, 5, 0), P(8, 0, 0)));
            Panel roofA = Wall(ids, RoofA, PanelType.Roof, Quad(P(0, 0, 3), P(8, 0, 3), P(8, 5, 3), P(0, 5, 3)));
            Panel southA = Wall(ids, ExtWallA, PanelType.WallExternal, Quad(P(0, 0, 0), P(8, 0, 0), P(8, 0, 3), P(0, 0, 3)));
            Window(ids, southA, GlazingDouble, P(2, 0, 0.9), P(6, 0, 0.9), P(6, 0, 2.4), P(2, 0, 2.4), new PartOOpeningProperties(0.62, 1.0, 30.0, OpeningRestriction.Unrestricted));
            Panel westA = Wall(ids, ExtWallA, PanelType.WallExternal, Quad(P(0, 5, 0), P(0, 0, 0), P(0, 0, 3), P(0, 5, 3)));
            Window(ids, westA, GlazingDouble, P(0, 1.5, 1), P(0, 3, 1), P(0, 3, 2.2), P(0, 1.5, 2.2));

            // ---- B: meeting, x 8..12, y 0..5, z 0..3
            Panel floorB = Wall(ids, FloorB, PanelType.SlabOnGrade, Quad(P(8, 0, 0), P(8, 5, 0), P(12, 5, 0), P(12, 0, 0)));
            Panel roofB = Wall(ids, RoofB, PanelType.Roof, Quad(P(8, 0, 3), P(12, 0, 3), P(12, 5, 3), P(8, 5, 3)));
            Panel southB = Wall(ids, ExtWallB, PanelType.WallExternal, Quad(P(8, 0, 0), P(12, 0, 0), P(12, 0, 3), P(8, 0, 3)));
            Window(ids, southB, GlazingSingle, P(9, 0, 1), P(9.8, 0, 1), P(9.8, 0, 2), P(9, 0, 2));
            Panel eastB = Wall(ids, ExtWallB, PanelType.WallExternal, Quad(P(12, 0, 0), P(12, 5, 0), P(12, 5, 3), P(12, 0, 3)));
            Window(ids, eastB, GlazingDouble, P(12, 1.5, 0.8), P(12, 3.5, 0.8), P(12, 3.5, 2.3), P(12, 1.5, 2.3), new PartOOpeningProperties(0.70, 0.6, 30.0, OpeningRestriction.NightClosed));
            Panel northB = Wall(ids, ExtWallB, PanelType.WallExternal, Quad(P(12, 5, 0), P(8, 5, 0), P(8, 5, 3), P(12, 5, 3)));

            // ---- C: store, x 0..8, y 5..8, z 0..4 (one metre taller than A)
            Panel floorC = Wall(ids, FloorA, PanelType.SlabOnGrade, Quad(P(0, 5, 0), P(0, 8, 0), P(8, 8, 0), P(8, 5, 0)));
            Panel roofC = Wall(ids, RoofC, PanelType.Roof, Quad(P(0, 5, 4), P(8, 5, 4), P(8, 8, 4), P(0, 8, 4)));
            Panel northC = Wall(ids, ExtWallC, PanelType.WallExternal, Quad(P(8, 8, 0), P(0, 8, 0), P(0, 8, 4), P(8, 8, 4)));
            Window(ids, northC, GlazingSingle, P(3, 8, 1.5), P(4.5, 8, 1.5), P(4.5, 8, 2.5), P(3, 8, 2.5));
            Panel westC = Wall(ids, ExtWallC, PanelType.WallExternal, Quad(P(0, 8, 0), P(0, 5, 0), P(0, 5, 4), P(0, 8, 4)));
            Panel eastC = Wall(ids, ExtWallC, PanelType.WallExternal, Quad(P(8, 5, 0), P(8, 8, 0), P(8, 8, 4), P(8, 5, 4)), adiabatic: true);
            Panel stripC = Wall(ids, ExtWallC, PanelType.WallExternal, Quad(P(0, 5, 3), P(8, 5, 3), P(8, 5, 4), P(0, 5, 4)));

            // ---- partitions
            Panel partitionAB = Wall(ids, PartitionAB, PanelType.WallInternal, Quad(P(8, 0, 0), P(8, 5, 0), P(8, 5, 3), P(8, 0, 3)));
            Panel partitionAC = Wall(ids, PartitionAC, PanelType.WallInternal, Quad(P(0, 5, 0), P(8, 5, 0), P(8, 5, 3), P(0, 5, 3)));

            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();

            // The model order is A, B, C (or C, B, A). It decides which side of each partition is "reversed".
            foreach (Space space in reverseModelOrder ? new[] { storeC, meetingB, officeA } : new[] { officeA, meetingB, storeC })
            {
                adjacencyCluster.AddObject(space);
            }

            // Every panel with the spaces it is related to, in the order the relations are stored.
            List<KeyValuePair<Panel, Space[]>> panels = new List<KeyValuePair<Panel, Space[]>>();
            foreach (Panel panel in new[] { floorA, roofA, southA, westA }) panels.Add(new KeyValuePair<Panel, Space[]>(panel, new[] { officeA }));
            foreach (Panel panel in new[] { floorB, roofB, southB, eastB, northB }) panels.Add(new KeyValuePair<Panel, Space[]>(panel, new[] { meetingB }));
            foreach (Panel panel in new[] { floorC, roofC, northC, westC, eastC, stripC }) panels.Add(new KeyValuePair<Panel, Space[]>(panel, new[] { storeC }));

            // A-B is stored with its relations B first; A-C in model order.
            panels.Add(new KeyValuePair<Panel, Space[]>(partitionAB, new[] { meetingB, officeA }));
            panels.Add(new KeyValuePair<Panel, Space[]>(partitionAC, new[] { officeA, storeC }));

            if (panelOrder == PanelOrder.Reversed)
            {
                panels.Reverse();
            }
            else if (panelOrder == PanelOrder.Rotated)
            {
                // The second half first: every panel's neighbours in the list change, and so does where each zone's panels start.
                panels = panels.Skip(panels.Count / 2).Concat(panels.Take(panels.Count / 2)).ToList();
            }

            foreach (KeyValuePair<Panel, Space[]> keyValuePair in panels)
            {
                adjacencyCluster.AddObject(keyValuePair.Key);
                foreach (Space space in keyValuePair.Value)
                {
                    adjacencyCluster.AddRelation(space, keyValuePair.Key);
                }
            }

            return new AnalyticalModel((reverseModelOrder ? "LoadSensitiveReversedOrder" : "LoadSensitive") + (panelOrder == PanelOrder.AsListed ? string.Empty : "Panels" + panelOrder), null, null, null, adjacencyCluster, Materials(), Profiles());
        }

        /// <summary>Expected plan-independent facts, so a test and the licensed comparison state them once.</summary>
        public static class Expected
        {
            public const int Spaces = 3;
            public const int Panels = 17;
            public const int Apertures = 5;
            public const double FloorAreaA = 40, FloorAreaB = 20, FloorAreaC = 24;
            public const double VolumeA = 120, VolumeB = 60, VolumeC = 96;

            public static IEnumerable<string> SpaceNames
            {
                get { return new[] { SpaceName_Office, SpaceName_Meeting, SpaceName_Store }; }
            }
        }
    }
}
