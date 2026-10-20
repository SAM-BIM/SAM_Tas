// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Geometry.Spatial;
using SAM.Weather;
using System.Collections.Generic;
using System.Linq;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>
    /// The disposable S08 PR-B calibration models. They exist to settle native conventions (host-surface area, layer
    /// orientation, frame and pane surfaces, slot and calendar alignment) BEFORE any fixture is compared, so a rule
    /// is never fitted to a fixture's answer. They are not equivalence fixtures (D-FIX, PR-C).
    /// <para>
    /// Every authored value is distinct (no two layers, gains or hours share a number) so that a swapped layer,
    /// slot, zone or gain cannot cancel out. Assembled by hand, like <see cref="SyntheticModels"/>, to avoid the
    /// <c>ActiveSetting</c> cold start in a bare console process.
    /// </para>
    /// </summary>
    public static class CalibrationModels
    {
        public const string Box = "calib-box";
        public const string TwoZones = "calib-two";
        public const string Align = "calib-align";

        public static IReadOnlyList<string> Names { get; } = new[] { Box, TwoZones, Align };

        public static AnalyticalModel Create(string name)
        {
            switch (name)
            {
                case Box:
                    return CreateBox();

                case TwoZones:
                    return CreateTwoZones();

                case Align:
                    return CreateAlign();

                default:
                    return null;
            }
        }

        // ---- materials and constructions: asymmetric on purpose --------------------------------------------------------

        private static OpaqueMaterial Opaque(string name, double conductivity, double specificHeat, double density, double extSolar, double intSolar, double extLight, double intLight, double extEmissivity, double intEmissivity)
        {
            OpaqueMaterial result = new OpaqueMaterial(name, "S08 calibration", name, name, conductivity, specificHeat, density);
            result.SetValue(OpaqueMaterialParameter.ExternalSolarReflectance, extSolar);
            result.SetValue(OpaqueMaterialParameter.InternalSolarReflectance, intSolar);
            result.SetValue(OpaqueMaterialParameter.ExternalLightReflectance, extLight);
            result.SetValue(OpaqueMaterialParameter.InternalLightReflectance, intLight);
            result.SetValue(OpaqueMaterialParameter.ExternalEmissivity, extEmissivity);
            result.SetValue(OpaqueMaterialParameter.InternalEmissivity, intEmissivity);
            result.SetValue(global::SAM.Analytical.MaterialParameter.VapourDiffusionFactor, 7.5);
            return result;
        }

        private static TransparentMaterial Glass(string name)
        {
            TransparentMaterial result = new TransparentMaterial(name, "S08 calibration", name, name, 1.05, 840, 2500);
            result.SetValue(TransparentMaterialParameter.SolarTransmittance, 0.781);
            result.SetValue(TransparentMaterialParameter.LightTransmittance, 0.882);
            result.SetValue(TransparentMaterialParameter.ExternalSolarReflectance, 0.071);
            result.SetValue(TransparentMaterialParameter.InternalSolarReflectance, 0.073);
            result.SetValue(TransparentMaterialParameter.ExternalLightReflectance, 0.081);
            result.SetValue(TransparentMaterialParameter.InternalLightReflectance, 0.083);
            result.SetValue(TransparentMaterialParameter.ExternalEmissivity, 0.837);
            result.SetValue(TransparentMaterialParameter.InternalEmissivity, 0.839);
            result.SetValue(global::SAM.Analytical.MaterialParameter.VapourDiffusionFactor, 9999);
            result.SetValue(TransparentMaterialParameter.IsBlind, false);
            return result;
        }

        private static MaterialLibrary Materials()
        {
            MaterialLibrary result = new MaterialLibrary("S08 calibration materials");
            result.Add(Opaque("CAL_Brick", 0.77, 800, 1700, 0.301, 0.352, 0.311, 0.362, 0.901, 0.902));
            result.Add(Opaque("CAL_Insulation", 0.035, 1400, 30, 0.401, 0.452, 0.411, 0.462, 0.903, 0.904));
            result.Add(Opaque("CAL_Plaster", 0.41, 840, 950, 0.701, 0.752, 0.711, 0.762, 0.881, 0.882));
            result.Add(Opaque("CAL_Screed", 1.13, 1000, 2000, 0.201, 0.252, 0.211, 0.262, 0.911, 0.912));
            result.Add(Opaque("CAL_Membrane", 0.23, 1500, 1100, 0.101, 0.152, 0.111, 0.162, 0.921, 0.922));
            result.Add(Opaque("CAL_Board", 0.13, 1600, 650, 0.501, 0.552, 0.511, 0.562, 0.861, 0.862));
            result.Add(Opaque("CAL_Frame", 0.16, 1880, 1390, 0.601, 0.652, 0.611, 0.662, 0.851, 0.852));
            result.Add(Glass("CAL_Glass"));
            return result;
        }

        public static Construction ExternalWall { get; } = new Construction(System.Guid.Parse("5080b000-0000-0000-0000-0000000000a1"), "CAL_EXT_WALL", new[] { new ConstructionLayer("CAL_Brick", 0.102), new ConstructionLayer("CAL_Insulation", 0.081), new ConstructionLayer("CAL_Plaster", 0.013) });
        public static Construction Roof { get; } = new Construction(System.Guid.Parse("5080b000-0000-0000-0000-0000000000a2"), "CAL_ROOF", new[] { new ConstructionLayer("CAL_Membrane", 0.006), new ConstructionLayer("CAL_Insulation", 0.152), new ConstructionLayer("CAL_Board", 0.018) });
        public static Construction GroundFloor { get; } = new Construction(System.Guid.Parse("5080b000-0000-0000-0000-0000000000a3"), "CAL_GRD_FLOOR", new[] { new ConstructionLayer("CAL_Insulation", 0.103), new ConstructionLayer("CAL_Screed", 0.071) });
        public static Construction Partition { get; } = new Construction(System.Guid.Parse("5080b000-0000-0000-0000-0000000000a4"), "CAL_PARTITION", new[] { new ConstructionLayer("CAL_Board", 0.0125), new ConstructionLayer("CAL_Insulation", 0.047), new ConstructionLayer("CAL_Plaster", 0.009) });

        public static ApertureConstruction FramedGlazing { get; } = CreateGlazing(System.Guid.Parse("5080b000-0000-0000-0000-0000000000b1"), "CAL_GLZ_FRAMED", true);
        public static ApertureConstruction UnframedGlazing { get; } = CreateGlazing(System.Guid.Parse("5080b000-0000-0000-0000-0000000000b2"), "CAL_GLZ_UNFRAMED", false);

        private static ApertureConstruction CreateGlazing(System.Guid guid, string name, bool frame)
        {
            ApertureConstruction result = new ApertureConstruction(guid, name, ApertureType.Window, new[] { new ConstructionLayer("CAL_Glass", 0.0061) }, frame ? new[] { new ConstructionLayer("CAL_Frame", 0.068) } : null);
            result.SetValue(ApertureConstructionParameter.Transparent, true);
            if (frame)
            {
                result.SetValue(ApertureConstructionParameter.DefaultFrameWidth, 0.052);
            }

            return result;
        }

        private static Point3D P(double x, double y, double z)
        {
            return new Point3D(x, y, z);
        }

        // ---- profiles ---------------------------------------------------------------------------------------------------

        private static double[] Daily(params double[] values)
        {
            return values;
        }

        // 24 distinct values in [low, high], none equal to another, so any slot shift changes every hour.
        private static double[] DistinctDay(double low, double high, int seed)
        {
            double[] result = new double[24];
            for (int h = 0; h < 24; h++)
            {
                result[h] = low + (high - low) * (((h * 7 + seed) % 24) + 0.5) / 24.0;
            }

            return result;
        }

        // 8760 distinct-by-day-and-hour fractions: the hour pattern of DistinctDay, scaled per day, so a one-day or one-hour shift is visible.
        private static double[] DistinctYear(int seed)
        {
            double[] day = DistinctDay(0.1, 0.9, seed);
            double[] result = new double[8760];
            for (int d = 0; d < 365; d++)
            {
                double scale = 0.6 + 0.4 * ((d * 37 + seed) % 365) / 365.0;
                for (int h = 0; h < 24; h++)
                {
                    result[d * 24 + h] = System.Math.Round(day[h] * scale, 6);
                }
            }

            return result;
        }

        private static Profile Profile(ProfileLibrary library, string name, ProfileType type, IEnumerable<double> values)
        {
            Profile result = new Profile(name, type, values);
            library.Add(result);
            return result;
        }

        // ---- internal conditions ----------------------------------------------------------------------------------------

        private sealed class Gains
        {
            public string Name;
            public double Occupancy, SensiblePerPerson, LatentPerPerson, LightingPerArea, EquipmentSensiblePerArea, EquipmentLatentPerArea, InfiltrationAch;
            public double LightingRadiant, OccupancyRadiant, EquipmentRadiant;
            public double SupplyPerPerson = double.NaN, SupplyPerArea = double.NaN;
            public bool VentilationProfile;
            public int Seed;
        }

        private static InternalCondition InternalCondition(Space space, ProfileLibrary library, Gains g)
        {
            InternalCondition ic = new InternalCondition(g.Name);
            ic.SetValue(InternalConditionParameter.OccupancySensibleGainPerPerson, g.SensiblePerPerson);
            ic.SetValue(InternalConditionParameter.OccupancyLatentGainPerPerson, g.LatentPerPerson);
            ic.SetValue(InternalConditionParameter.LightingGainPerArea, g.LightingPerArea);
            ic.SetValue(InternalConditionParameter.EquipmentSensibleGainPerArea, g.EquipmentSensiblePerArea);
            ic.SetValue(InternalConditionParameter.EquipmentLatentGainPerArea, g.EquipmentLatentPerArea);
            ic.SetValue(InternalConditionParameter.InfiltrationAirChangesPerHour, g.InfiltrationAch);
            ic.SetValue(InternalConditionParameter.LightingRadiantProportion, g.LightingRadiant);
            ic.SetValue(InternalConditionParameter.OccupancyRadiantProportion, g.OccupancyRadiant);
            ic.SetValue(InternalConditionParameter.EquipmentRadiantProportion, g.EquipmentRadiant);
            if (!double.IsNaN(g.SupplyPerPerson)) ic.SetValue(InternalConditionParameter.SupplyAirFlowPerPerson, g.SupplyPerPerson);
            if (!double.IsNaN(g.SupplyPerArea)) ic.SetValue(InternalConditionParameter.SupplyAirFlowPerArea, g.SupplyPerArea);

            string p = g.Name + "_";
            // One 24-hour, one 8760-hour and one single-value gain profile, so all three TBD profile types are read back.
            ic.SetValue(InternalConditionParameter.OccupancyProfileName, Profile(library, p + "OCC", ProfileType.Occupancy, DistinctDay(0.05, 0.95, g.Seed + 1)).Name);
            ic.SetValue(InternalConditionParameter.LightingProfileName, Profile(library, p + "LGT", ProfileType.Lighting, DistinctDay(0.1, 1.0, g.Seed + 2)).Name);
            ic.SetValue(InternalConditionParameter.EquipmentSensibleProfileName, Profile(library, p + "EQS", ProfileType.EquipmentSensible, DistinctYear(g.Seed + 3)).Name);
            ic.SetValue(InternalConditionParameter.EquipmentLatentProfileName, Profile(library, p + "EQL", ProfileType.EquipmentLatent, new[] { 0.63 + 0.01 * g.Seed }).Name);
            ic.SetValue(InternalConditionParameter.InfiltrationProfileName, Profile(library, p + "INF", ProfileType.Infiltration, DistinctDay(0.2, 1.0, g.Seed + 4)).Name);
            ic.SetValue(InternalConditionParameter.HeatingProfileName, Profile(library, p + "HTG", ProfileType.Heating, DistinctDay(15.0, 21.0, g.Seed + 5)).Name);
            ic.SetValue(InternalConditionParameter.CoolingProfileName, Profile(library, p + "CLG", ProfileType.Cooling, DistinctDay(24.0, 28.0, g.Seed + 6)).Name);
            ic.SetValue(InternalConditionParameter.HumidificationProfileName, Profile(library, p + "HUM", ProfileType.Humidification, new[] { 31.0 + g.Seed }).Name);
            ic.SetValue(InternalConditionParameter.DehumidificationProfileName, Profile(library, p + "DHU", ProfileType.Dehumidification, new[] { 69.0 + g.Seed }).Name);
            if (g.VentilationProfile)
            {
                ic.SetValue(InternalConditionParameter.VentilationProfileName, Profile(library, p + "VNT", ProfileType.Ventilation, DistinctDay(0.3, 1.0, g.Seed + 7)).Name);
            }

            space.SetValue(global::SAM.Analytical.SpaceParameter.Occupancy, g.Occupancy);
            space.InternalCondition = ic;
            return ic;
        }

        private static void Stored(Space space, double area, double volume)
        {
            space.SetValue(global::SAM.Analytical.SpaceParameter.Area, area);
            space.SetValue(global::SAM.Analytical.SpaceParameter.Volume, volume);
        }

        private static AnalyticalModel Model(string name, AdjacencyCluster adjacencyCluster, ProfileLibrary profileLibrary)
        {
            return new AnalyticalModel(name, null, null, null, adjacencyCluster, Materials(), profileLibrary);
        }

        // ---- the models -------------------------------------------------------------------------------------------------

        /// <summary>
        /// 5 x 4 x 3 m, asymmetric three-layer walls, a 2 x 1 m framed window (south) and a 1 x 1 m unframed window (east),
        /// every S07 gain, setpoint, infiltration and a V2 ventilation delivery. Stored Area 20 m2, Volume 60 m3 = geometry.
        /// </summary>
        public static AnalyticalModel CreateBox()
        {
            ProfileLibrary library = new ProfileLibrary("S08 calibration profiles");
            Space space = new Space(System.Guid.Parse("5080b000-0000-0000-0000-00000000c001"), "CalBox", P(2.5, 2, 1.5));
            Stored(space, 20, 60);

            List<Panel> panels = SyntheticModels.BoxPanels(0, 5, 0, 4, 0, 3, GroundFloor, PanelType.SlabOnGrade, Roof, PanelType.Roof, ExternalWall, PanelType.WallExternal);
            panels[2].AddAperture(Analytical.Create.Aperture(FramedGlazing, SyntheticModels.Quad(P(1.5, 0, 1), P(3.5, 0, 1), P(3.5, 0, 2), P(1.5, 0, 2))));
            panels[3].AddAperture(Analytical.Create.Aperture(UnframedGlazing, SyntheticModels.Quad(P(5, 1.5, 1), P(5, 2.5, 1), P(5, 2.5, 2), P(5, 1.5, 2))));

            InternalCondition(space, library, new Gains
            {
                Name = "CAL_IC_BOX", Seed = 1, Occupancy = 2.1, SensiblePerPerson = 73.5, LatentPerPerson = 54.5, LightingPerArea = 8.25,
                EquipmentSensiblePerArea = 11.75, EquipmentLatentPerArea = 1.55, InfiltrationAch = 0.35,
                LightingRadiant = 0.42, OccupancyRadiant = 0.27, EquipmentRadiant = 0.33,
                SupplyPerPerson = 0.0105, VentilationProfile = true
            });

            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();
            adjacencyCluster.AddObject(space);
            foreach (Panel panel in panels)
            {
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(space, panel);
            }

            return Model(Box, adjacencyCluster, library);
        }

        /// <summary>
        /// Two 5 x 4 x 3 m zones (A: x 0..5, B: x 5..10) sharing one asymmetric partition, distinct internal conditions:
        /// A is V1 (a requirement, no Ventilation profile), B is V0 (no requirement). The internal-layer orientation case.
        /// </summary>
        public static AnalyticalModel CreateTwoZones()
        {
            ProfileLibrary library = new ProfileLibrary("S08 calibration profiles");
            Space a = new Space(System.Guid.Parse("5080b000-0000-0000-0000-00000000c0a1"), "CalA", P(2.5, 2, 1.5));
            Space b = new Space(System.Guid.Parse("5080b000-0000-0000-0000-00000000c0b1"), "CalB", P(7.5, 2, 1.5));
            Stored(a, 20, 60);
            Stored(b, 20, 60);

            List<Panel> panels_A = SyntheticModels.BoxPanels(0, 5, 0, 4, 0, 3, GroundFloor, PanelType.SlabOnGrade, Roof, PanelType.Roof, ExternalWall, PanelType.WallExternal);
            List<Panel> panels_B = SyntheticModels.BoxPanels(5, 10, 0, 4, 0, 3, GroundFloor, PanelType.SlabOnGrade, Roof, PanelType.Roof, ExternalWall, PanelType.WallExternal);
            Panel partition = SyntheticModels.Panel(Partition, PanelType.WallInternal, SyntheticModels.Quad(P(5, 0, 0), P(5, 4, 0), P(5, 4, 3), P(5, 0, 3)));
            panels_A.RemoveAt(3); // east of A
            panels_B.RemoveAt(5); // west of B
            panels_A[2].AddAperture(Analytical.Create.Aperture(FramedGlazing, SyntheticModels.Quad(P(1, 0, 0.9), P(2.5, 0, 0.9), P(2.5, 0, 2.1), P(1, 0, 2.1))));

            InternalCondition(a, library, new Gains
            {
                Name = "CAL_IC_A", Seed = 2, Occupancy = 1.3, SensiblePerPerson = 71.5, LatentPerPerson = 52.5, LightingPerArea = 7.25,
                EquipmentSensiblePerArea = 9.75, EquipmentLatentPerArea = 1.25, InfiltrationAch = 0.25,
                LightingRadiant = 0.44, OccupancyRadiant = 0.29, EquipmentRadiant = 0.31,
                SupplyPerArea = 0.00135, VentilationProfile = false
            });

            InternalCondition(b, library, new Gains
            {
                Name = "CAL_IC_B", Seed = 3, Occupancy = 3.7, SensiblePerPerson = 67.5, LatentPerPerson = 48.5, LightingPerArea = 5.75,
                EquipmentSensiblePerArea = 14.25, EquipmentLatentPerArea = 2.05, InfiltrationAch = 0.45,
                LightingRadiant = 0.38, OccupancyRadiant = 0.23, EquipmentRadiant = 0.37,
                VentilationProfile = false
            });

            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();
            adjacencyCluster.AddObject(a);
            adjacencyCluster.AddObject(b);
            foreach (Panel panel in panels_A)
            {
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(a, panel);
            }

            foreach (Panel panel in panels_B)
            {
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(b, panel);
            }

            adjacencyCluster.AddObject(partition);
            adjacencyCluster.AddRelation(a, partition);
            adjacencyCluster.AddRelation(b, partition);

            return Model(TwoZones, adjacencyCluster, library);
        }

        /// <summary>
        /// The P-ALIGN probe model: one closed box, no glazing, zero solar weather, and pulse schedules whose only
        /// non-zero hours name the slot TAS applies them in: lighting (24-hour profile) at SAM hour 10 of every day, sensible
        /// equipment (8760-hour profile) at day 100 hour 14 only, infiltration (24-hour) at hour 20, a heating setpoint step
        /// (24-hour thermostat profile) at hour 4. Its only use is the one approved 365-day alignment simulation.
        /// </summary>
        public static AnalyticalModel CreateAlign()
        {
            ProfileLibrary library = new ProfileLibrary("S08 P-ALIGN profiles");
            Space space = new Space(System.Guid.Parse("5080b000-0000-0000-0000-00000000c0c1"), "CalAlign", P(2.5, 2, 1.5));
            Stored(space, 20, 60);

            List<Panel> panels = SyntheticModels.BoxPanels(0, 5, 0, 4, 0, 3, GroundFloor, PanelType.SlabOnGrade, Roof, PanelType.Roof, ExternalWall, PanelType.WallExternal);

            InternalCondition ic = new InternalCondition("CAL_IC_ALIGN");
            ic.SetValue(InternalConditionParameter.LightingGainPerArea, 10.0);
            ic.SetValue(InternalConditionParameter.EquipmentSensibleGainPerArea, 10.0);
            ic.SetValue(InternalConditionParameter.InfiltrationAirChangesPerHour, 2.0);
            ic.SetValue(InternalConditionParameter.LightingRadiantProportion, 0.4);
            ic.SetValue(InternalConditionParameter.OccupancyRadiantProportion, 0.2);
            ic.SetValue(InternalConditionParameter.EquipmentRadiantProportion, 0.3);

            double[] lighting = new double[24];
            lighting[PulseLightingHour] = 1;
            double[] equipment = new double[8760];
            equipment[PulseEquipmentHourOfYear] = 1;
            double[] infiltration = new double[24];
            infiltration[PulseInfiltrationHour] = 1;
            double[] heating = Enumerable.Repeat(-20.0, 24).ToArray();
            heating[PulseHeatingHour] = 40;

            ic.SetValue(InternalConditionParameter.LightingProfileName, Profile(library, "ALIGN_LGT", ProfileType.Lighting, lighting).Name);
            ic.SetValue(InternalConditionParameter.EquipmentSensibleProfileName, Profile(library, "ALIGN_EQS", ProfileType.EquipmentSensible, equipment).Name);
            ic.SetValue(InternalConditionParameter.InfiltrationProfileName, Profile(library, "ALIGN_INF", ProfileType.Infiltration, infiltration).Name);
            ic.SetValue(InternalConditionParameter.HeatingProfileName, Profile(library, "ALIGN_HTG", ProfileType.Heating, heating).Name);
            ic.SetValue(InternalConditionParameter.CoolingProfileName, Profile(library, "ALIGN_CLG", ProfileType.Cooling, new[] { 60.0 }).Name);
            space.InternalCondition = ic;

            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();
            adjacencyCluster.AddObject(space);
            foreach (Panel panel in panels)
            {
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(space, panel);
            }

            return Model(Align, adjacencyCluster, library);
        }

        public const int PulseLightingHour = 10;
        public const int PulseEquipmentHourOfYear = 99 * 24 + 14;
        public const int PulseInfiltrationHour = 20;
        public const int PulseHeatingHour = 4;

        /// <summary>
        /// A 365-day synthetic year whose dry-bulb value names its own hour: 5 + (hour of day) + 0.01 x (day mod 10) degC,
        /// so the TSD hour a weather slot lands in is read off the value. No sun (global and diffuse 0), still air, fixed
        /// humidity. Used by every calibration run so the design days are derived from a known year.
        /// </summary>
        public static WeatherData CreateWeather()
        {
            WeatherYear weatherYear = new WeatherYear(2026);
            for (int d = 0; d < 365; d++)
            {
                WeatherDay day = new WeatherDay();
                for (int h = 0; h < 24; h++)
                {
                    day[WeatherDataType.DryBulbTemperature, h] = MarkerDryBulb(d, h);
                    day[WeatherDataType.RelativeHumidity, h] = 55 + (h % 6);
                    day[WeatherDataType.GlobalSolarRadiation, h] = 0;
                    day[WeatherDataType.DiffuseSolarRadiation, h] = 0;
                    day[WeatherDataType.CloudCover, h] = 0.5;
                    day[WeatherDataType.WindSpeed, h] = 2 + 0.1 * (h % 5);
                    day[WeatherDataType.WindDirection, h] = 180 + h;
                }

                weatherYear[d] = day;
            }

            WeatherData result = new WeatherData("S08 marker year", "S08 PR-B synthetic calibration weather (no sun)", 51.5, -0.12, 25, weatherYear);
            result.SetValue(WeatherDataParameter.TimeZone, "0");

            // Ground temperatures at 0.5 m and 2 m, so the Tas writer's 2 m preference (and its 12-month mean) is exercised.
            SAMCollection<GroundTemperature> groundTemperatures = new SAMCollection<GroundTemperature>
            {
                new GroundTemperature(0.5, 2.42, 1840, 860, 3.1, 2.9, 4.2, 6.6, 10.1, 13.4, 15.9, 16.8, 15.5, 12.6, 9.0, 5.7),
                new GroundTemperature(2.0, 2.42, 1840, 860, 6.21, 5.32, 5.61, 6.73, 8.83, 11.14, 13.05, 14.16, 14.27, 13.18, 11.09, 8.4)
            };
            result.SetValue(WeatherDataParameter.GroundTemperatures, groundTemperatures);
            return result;
        }

        public static double MarkerDryBulb(int day, int hour)
        {
            return 5 + hour + 0.01 * (day % 10);
        }
    }
}
