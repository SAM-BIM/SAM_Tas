// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Benchmark;
using SAM.Analytical.Tas.Benchmark;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using SAM.Analytical.Tas.DirectT3D.Validation.Results;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// A consistent synthetic S10 triple, built without Tas: raw TSD arrays and a raw TBD read (the reference inputs), the
    /// SAM model results exactly as the importer's documented semantics would store them, the model-level result as the
    /// converter computes it, and the B1a document from the real producer mapping (<c>ToBenchmark</c>). It exercises the
    /// cases the licensed fixtures may not: a peak in the last hour, a later tied maximum, an all-zero load, a design day
    /// that governs and a zero design day. The design-day series have the calendar shape measured on the S10 revision-2
    /// acc1 TSD: 8760 elements, the 24 hours of the TBD design day's year day populated, Tas's -1 everywhere else.
    /// </summary>
    public static class S10SyntheticTriple
    {
        public const string ZoneA = "S10 Zone A";
        public const string ZoneB = "S10 Zone B";
        public static readonly Guid GuidA = new Guid("a1000000-0000-0000-0000-0000000000aa");
        public static readonly Guid GuidB = new Guid("b2000000-0000-0000-0000-0000000000bb");
        public static readonly Guid SpaceA = new Guid("a1000000-0000-0000-0000-0000000005aa");
        public static readonly Guid SpaceB = new Guid("b2000000-0000-0000-0000-0000000005bb");

        public const int HeatingPeakHourA = 8759;
        public const int CoolingTieA1 = 4000, CoolingTieA2 = 4010;

        /// <summary>The TBD design days' year days (as read on the acc1 box TBD) and the first position of each day's block.</summary>
        public const int CoolingYearDay = 182, HeatingYearDay = 39;
        public static int Start(string role) => ResultReference.DayStart(role == ResultReference.Cooling ? CoolingYearDay : HeatingYearDay);

        public static double LowerLimit(int hour) => hour % 24 < 7 || hour % 24 >= 19 ? 16 : 21;
        public const double UpperLimit = 26;

        /// <summary>The hourly lighting schedule of every internal condition (TBD ticLG shape; factor = W/m2), hour-varying so it can place a design day in time.</summary>
        public static double LightingShape(int hour) => hour % 24 >= 8 && hour % 24 < 18 ? 0.8 : 0.1;
        public const double LightingFactor = 10;

        public sealed class Zone
        {
            public string Name;
            public Guid Guid, Space;
            public double FloorArea, Volume;
            public double[] Heating = new double[8760], Cooling = new double[8760], DryBulb = new double[8760];
            public double[] DesignHeating = new double[24], DesignCooling = new double[24];
            public double MaxHeating, MaxCooling;
        }

        /// <param name="heatingFullDay">True: every hour of each zone's heating design day holds its maximum (a 24-way tie, as on the acc1 box).</param>
        public static List<Zone> Zones(bool heatingFullDay = false)
        {
            Zone a = new Zone { Name = ZoneA, Guid = GuidA, Space = SpaceA, FloorArea = 20, Volume = 60, MaxHeating = 5500, MaxCooling = 2100 };
            Zone b = new Zone { Name = ZoneB, Guid = GuidB, Space = SpaceB, FloorArea = 30, Volume = 90, MaxHeating = 1300, MaxCooling = 0 };
            for (int h = 0; h < 8760; h++)
            {
                int day = h / 24, hour = h % 24;
                bool winter = day < 90 || day >= 300, summer = day >= 150 && day < 240;
                a.Heating[h] = winter ? 100 + 2 * hour : 0;
                a.Cooling[h] = summer && hour >= 12 && hour <= 16 ? 300 + 10 * (hour - 12) : 0;
                a.DryBulb[h] = 21;
                b.Heating[h] = winter ? 50 + hour : 0;
                b.DryBulb[h] = 20;
            }

            a.Heating[HeatingPeakHourA] = 5000;
            a.Cooling[CoolingTieA1] = 2000;
            a.Cooling[CoolingTieA2] = 2000;
            for (int h = 100; h < 105; h++) a.DryBulb[h] = 15;
            for (int h = 5000; h < 5003; h++) a.DryBulb[h] = 27;
            b.Heating[30] = 800;

            a.DesignHeating[7] = 900;
            a.DesignHeating[8] = 400;
            a.DesignCooling[14] = 1500;
            b.DesignHeating[6] = 1200;
            if (heatingFullDay)
            {
                a.DesignHeating = Enumerable.Repeat(900.0, 24).ToArray();
                b.DesignHeating = Enumerable.Repeat(1200.0, 24).ToArray();
            }

            return new List<Zone> { a, b };
        }

        private static ResultSeries Series(double[] values) => ResultSeries.Of((double[])values.Clone(), 1);

        /// <summary>A calendar-shaped design-day series: the 24 values at the role's year day, Tas's -1 at the other 8736 elements.</summary>
        public static ResultSeries Calendar(double[] day, string role)
        {
            double[] values = Enumerable.Repeat(ResultReference.DesignOutsideSentinel, 8760).ToArray();
            Array.Copy(day, 0, values, Start(role), 24);
            return ResultSeries.Of(values, 1);
        }

        /// <summary>The design-day peak Tas reports: max and the CLR index of its first maximum in the calendar-shaped series (0 for a zero peak).</summary>
        private static (double value, int index) NativeDesignPeak(double[] day, string role)
        {
            double max = day.Max();
            return max == 0 ? (0, 0) : (max, Start(role) + Array.IndexOf(day, max) + 1);
        }

        /// <summary>A GetPeakZoneGains-like table: row 0 zone number, row 1 the value, row 2 the CLR index (0 for a zero peak), lower bounds 0.</summary>
        private static NativeTable Table(IList<(double value, int index)> columns)
        {
            NativeTable table = new NativeTable { Kind = FieldKind.Finite, LowerBounds = new[] { 0, 0 }, Lengths = new[] { 3, columns.Count } };
            table.Cells.Add(columns.Select((x, i) => NativeField.Of(i + 1)).ToList());
            table.Cells.Add(columns.Select(x => NativeField.Of(x.value)).ToList());
            table.Cells.Add(columns.Select(x => NativeField.Of(x.index)).ToList());
            return table;
        }

        /// <summary>The peak Tas reports: max and the CLR index (lower bound 1) of the chosen tie; zone A cooling reports the later tie.</summary>
        private static (double value, int index) NativePeak(Zone zone, double[] values)
        {
            double max = values.Max();
            if (max == 0) return (0, 0);
            int position = values == zone.Cooling && zone.Name == ZoneA ? CoolingTieA2 : Array.IndexOf(values, max);
            return (max, position + 1);
        }

        public static TsdResultSnapshot Tsd(List<Zone> zones, bool designDays = true)
        {
            TsdResultSnapshot result = new TsdResultSnapshot { Path = "synthetic.tsd", Sha256Before = "synthetic", Sha256After = "synthetic", FirstDay = NativeField.Of(1), LastDay = NativeField.Of(365) };
            double[] heating = new double[8760], cooling = new double[8760];
            int index = 1;
            foreach (Zone zone in zones)
            {
                TsdResultZone item = new TsdResultZone { Index = index++, Number = NativeField.Of(index - 1), Name = zone.Name, Guid = zone.Guid.ToString("B").ToUpperInvariant(), FloorArea = NativeField.Of(zone.FloorArea), Volume = NativeField.Of(zone.Volume) };
                item.Arrays["heatingLoad"] = Series(zone.Heating);
                item.Arrays["coolingLoad"] = Series(zone.Cooling);
                item.Arrays["dryBulbTemp"] = Series(zone.DryBulb);
                double[] lighting = Enumerable.Repeat(50.0, 8760).ToArray();
                double[] transfer = new double[8760];
                for (int h = 0; h < 8760; h++)
                {
                    transfer[h] = -(zone.Heating[h] - zone.Cooling[h]) - lighting[h];
                    heating[h] += zone.Heating[h];
                    cooling[h] += zone.Cooling[h];
                }

                foreach (string term in ResultReference.BalanceTerms)
                {
                    item.Arrays[term] = Series(term == "lightingGain" ? lighting : term == "buildingHeatTransfer" ? transfer : new double[8760]);
                }

                result.Zones.Add(item);
            }

            result.Building["heatingProfile"] = Series(heating);
            result.Building["coolingProfile"] = Series(cooling);
            result.AnnualPeakZoneGains["heatingLoad"] = Table(zones.Select(x => NativePeak(x, x.Heating)).ToList());
            result.AnnualPeakZoneGains["coolingLoad"] = Table(zones.Select(x => NativePeak(x, x.Cooling)).ToList());

            foreach (string role in designDays ? new[] { ResultReference.Cooling, ResultReference.Heating } : new string[0])
            {
                TsdDesignDataset dataset = new TsdDesignDataset { Role = role, Index = 0, Name = role == ResultReference.Cooling ? "CDD" : "HDD" };
                int i = 1;
                foreach (Zone zone in zones)
                {
                    TsdResultZone item = new TsdResultZone { Index = i++, Name = zone.Name, Guid = zone.Guid.ToString("B").ToUpperInvariant() };
                    item.Arrays["heatingLoad"] = Calendar(role == ResultReference.Heating ? zone.DesignHeating : new double[24], role);
                    item.Arrays["coolingLoad"] = Calendar(role == ResultReference.Cooling ? zone.DesignCooling : new double[24], role);
                    item.Arrays["dryBulbTemp"] = Calendar(Enumerable.Repeat(20.0, 24).ToArray(), role);
                    item.Arrays["lightingGain"] = Calendar(Enumerable.Range(0, 24).Select(h => zone.FloorArea * LightingFactor * LightingShape(h)).ToArray(), role);
                    item.Arrays["equipmentSensibleGain"] = Calendar(new double[24], role);
                    dataset.Zones.Add(item);
                }

                dataset.PeakZoneGains = Table(zones.Select(x => NativeDesignPeak(role == ResultReference.Heating ? x.DesignHeating : x.DesignCooling, role)).ToList());
                result.DesignDatasets.Add(dataset);
            }

            return result;
        }

        /// <summary>
        /// The TBD design-day coverage: one cooling group "CDD" and one heating group "HDD" (one day each, day types CDD and
        /// HDD, 1 repetition), or, without design days, a completely read TBD with no group.
        /// </summary>
        public static TbdDesignCoverage Coverage(bool designDays = true)
        {
            TbdDesignCoverage result = new TbdDesignCoverage { Path = "synthetic.tbd", Sha256Before = "synthetic", Sha256After = "synthetic" };
            if (!designDays) return result;
            result.Groups.Add(new DesignCoverageGroup { Role = ResultReference.Cooling, Index = 0, Name = "CDD", Days = { new DesignCoverageDay { Index = 0, Name = "CDD day", DayType = "CDD", Repetitions = NativeField.Of(1) } } });
            result.Groups.Add(new DesignCoverageGroup { Role = ResultReference.Heating, Index = 0, Name = "HDD", Days = { new DesignCoverageDay { Index = 0, Name = "HDD day", DayType = "HDD", Repetitions = NativeField.Of(1) } } });
            return result;
        }

        /// <param name="heatingTimeSignal">False gives the heating design day's condition no gains, as in the S08 PR-C fixtures.</param>
        public static NativeSnapshot Tbd(List<Zone> zones, bool heatingTimeSignal = true)
        {
            NativeSnapshot result = new NativeSnapshot { Calendar = new NativeCalendar() };
            result.Reader.TbdSha256Before = result.Reader.TbdSha256After = "synthetic";
            for (int d = 1; d <= 365; d++) result.Calendar.Days.Add(new NativeCalendarDay { Index = d, DayType = "Weekday" });
            foreach (Zone zone in zones)
            {
                NativeInternalCondition ic = new NativeInternalCondition { Name = "IC " + zone.Name, DayTypes = new List<string> { "Weekday", "Saturday", "Sunday", "CDD" } };
                ic.ThermostatProfiles["ticLL"] = Hourly(LowerLimit);
                ic.ThermostatProfiles["ticUL"] = Hourly(h => UpperLimit);
                ic.GainProfiles["ticLG"] = Hourly(LightingShape, LightingFactor);
                NativeInternalCondition hdd = new NativeInternalCondition { Name = "IC " + zone.Name + " - HDD", DayTypes = new List<string> { "HDD" } };
                hdd.GainProfiles["ticLG"] = heatingTimeSignal ? Hourly(LightingShape, LightingFactor) : Hourly(h => 0);
                result.Zones.Add(new NativeZone { Name = zone.Name, Guid = zone.Guid.ToString("D"), MaxHeatingLoad = NativeField.Of(zone.MaxHeating), MaxCoolingLoad = NativeField.Of(zone.MaxCooling), InternalConditions = { ic, hdd } });
            }

            // The raw TBD design days, as TbdReadBack records them (0-based indices), with their year days.
            result.CoolingDesignDays.Add(new NativeDesignDayGroup { Index = 0, Name = "CDD", Days = { new NativeDesignDay { Index = 0, Name = "CDD day", YearDay = NativeField.Of(CoolingYearDay), Repetitions = NativeField.Of(1), DayType = "CDD" } } });
            result.HeatingDesignDays.Add(new NativeDesignDayGroup { Index = 0, Name = "HDD", Days = { new NativeDesignDay { Index = 0, Name = "HDD day", YearDay = NativeField.Of(HeatingYearDay), Repetitions = NativeField.Of(1), DayType = "HDD" } } });
            return result;
        }

        private static NativeProfile Hourly(Func<int, double> value, double factor = 1)
        {
            NativeProfile profile = new NativeProfile { Kind = FieldKind.Finite, Type = "hourly", TypeCode = 2, Factor = NativeField.Of(factor), Hourly = new Dictionary<int, NativeField>() };
            for (int slot = 1; slot <= 24; slot++) profile.Hourly[slot] = NativeField.Of(value(slot - 1));
            return profile;
        }

        // ---- the subject, as the importer and the converter define it --------------------------------------------------

        private static int Unmet(Zone zone, bool heating)
        {
            int count = 0;
            for (int h = 0; h < 8760; h++)
            {
                if (heating ? zone.DryBulb[h] + 0.5 < LowerLimit(h) : zone.DryBulb[h] - 0.5 > UpperLimit) count++;
            }

            return count;
        }

        private static (double max, int first) Max(double[] values)
        {
            double max = values.Max();
            return (max, Array.IndexOf(values, max));
        }

        private static SpaceSimulationResult Result(Zone zone, LoadType loadType, int annualHour, bool designDays)
        {
            bool heating = loadType == LoadType.Heating;
            double[] annual = heating ? zone.Heating : zone.Cooling, design = designDays ? (heating ? zone.DesignHeating : zone.DesignCooling) : null;
            (double annualMax, _) = Max(annual);
            (double designMax, int designPosition) = design == null ? (double.NaN, -1) : Max(design);

            SpaceSimulationResult result = new SpaceSimulationResult(zone.Name, ResultReference.TasSource, zone.Guid.ToString("B").ToUpperInvariant());
            result.SetValue(SpaceSimulationResultParameter.LoadType, loadType.Text());
            result.SetValue(SpaceSimulationResultParameter.AnnualPeak, annualMax == 0 ? new SpaceLoadPeak(LoadPeakBasis.AnnualSimulation, 0) : new SpaceLoadPeak(LoadPeakBasis.AnnualSimulation, annualMax) { HourOfYear = annualHour, HourOfDay = annualHour % 24 });
            if (designDays) result.SetValue(SpaceSimulationResultParameter.DesignDayPeak, designMax == 0 ? new SpaceLoadPeak(LoadPeakBasis.DesignDay, 0) { DesignDayName = heating ? "HDD" : "CDD" } : new SpaceLoadPeak(LoadPeakBasis.DesignDay, designMax) { HourOfDay = designPosition % 24, DesignDayName = heating ? "HDD" : "CDD" });

            bool designGoverns = designDays && !(annualMax > designMax);
            double governing = designGoverns ? designMax : annualMax;
            // Tas's raw index: a design day's is the CLR index in its calendar-shaped series.
            int index = governing == 0 ? 0 : designGoverns ? Start(heating ? ResultReference.Heating : ResultReference.Cooling) + designPosition + 1 : annualHour + 1;
            result.SetValue(SpaceSimulationResultParameter.SizingMethod, (designGoverns ? (heating ? SizingMethod.HDD : SizingMethod.CDD) : SizingMethod.Simulation).Text());
            result.SetValue(SpaceSimulationResultParameter.Load, governing == 0 ? -1.0 : governing);
            result.SetValue(SpaceSimulationResultParameter.LoadIndex, index);
            result.SetValue(SpaceSimulationResultParameter.DesignLoad, heating ? zone.MaxHeating : zone.MaxCooling);
            result.SetValue(SpaceSimulationResultParameter.UnmetHours, Unmet(zone, heating));
            return result;
        }

        public static AnalyticalModel Model(List<Zone> zones, bool designDays = true)
        {
            AdjacencyCluster cluster = new AdjacencyCluster();
            foreach (Zone zone in zones)
            {
                Space space = new Space(zone.Space, zone.Name, new Point3D(0, 0, 0));
                space.SetValue(SpaceParameter.Area, zone.FloorArea);
                space.SetValue(SpaceParameter.Volume, zone.Volume);
                cluster.AddObject(space);

                // Zone A cooling has two tied maxima: the importer may hold either; this one holds the later.
                int heatingHour = Max(zone.Heating).first, coolingHour = zone.Name == ZoneA ? CoolingTieA2 : Max(zone.Cooling).first;
                foreach (SpaceSimulationResult result in new[] { Result(zone, LoadType.Heating, heatingHour, designDays), Result(zone, LoadType.Cooling, coolingHour, designDays) })
                {
                    cluster.AddObject(result);
                    cluster.AddRelation(space, result);
                }
            }

            return new AnalyticalModel("S10 synthetic", "S10 offline triple", null, null, cluster, new Core.MaterialLibrary("S10"), new ProfileLibrary("S10"));
        }

        /// <summary>The model-level result exactly as <c>ToSAM_AnalyticalModelSimulationResult</c> computes it.</summary>
        public static AnalyticalModelSimulationResult ModelResult(List<Zone> zones)
        {
            double[] heating = new double[8760], cooling = new double[8760];
            foreach (Zone zone in zones)
            {
                for (int h = 0; h < 8760; h++)
                {
                    heating[h] += zone.Heating[h];
                    cooling[h] += zone.Cooling[h];
                }
            }

            AnalyticalModelSimulationResult result = new AnalyticalModelSimulationResult("S10 synthetic", ResultReference.TasSource, "synthetic.tsd");
            result.SetValue(AnalyticalModelSimulationResultParameter.ConsumptionHeating, heating.Sum() / 1000);
            result.SetValue(AnalyticalModelSimulationResultParameter.PeakHeatingLoad, heating.Max() / 1000);
            result.SetValue(AnalyticalModelSimulationResultParameter.PeakHeatingHour, Array.IndexOf(heating, heating.Max()));
            result.SetValue(AnalyticalModelSimulationResultParameter.ConsumptionCooling, cooling.Sum() / 1000);
            result.SetValue(AnalyticalModelSimulationResultParameter.PeakCoolingLoad, cooling.Max() / 1000);
            result.SetValue(AnalyticalModelSimulationResultParameter.PeakCoolingHour, Array.IndexOf(cooling, cooling.Max()));
            result.SetValue(AnalyticalModelSimulationResultParameter.FloorArea, zones.Sum(x => x.FloorArea));
            result.SetValue(AnalyticalModelSimulationResultParameter.Volume, zones.Sum(x => x.Volume));
            result.SetResultUnitsVersion();
            return result;
        }

        public static string B1a(AnalyticalModel model, AnalyticalModelSimulationResult modelResult)
        {
            TasBenchmarkContext context = new TasBenchmarkContext
            {
                SourceModelName = "S10 synthetic",
                SourceModelGuid = "a1000000000000000000000000000510",
                SourceFileHash = BenchmarkHash.ComputeSha256(new byte[] { 1, 0 }),
                CanonicalModelHash = BenchmarkCanonicalJson.ComputeSha256("{\"model\":\"s10-synthetic\"}"),
                CanonicalizationVersion = BenchmarkCanonicalization.CurrentVersion,
                SamCommit = "0123456789abcdef",
                RunnerCommit = "fedcba9876543210",
                EngineName = "Tas",
                EngineVersion = "synthetic",
                WeatherIdentity = "S10_Synthetic_Weather",
                WeatherHash = BenchmarkHash.ComputeSha256(new byte[] { 5, 1, 0 }),
                DesignDaySource = DesignDaySource.EmbeddedModel,
                DurationSeconds = 1.0,
                State = RunState.Success,
                RunTimestampUtc = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero),
                ModelResult = modelResult,
            };

            return BenchmarkSerializer.Serialize(model.ToBenchmark(context));
        }

        /// <summary>The serialised inputs of the consistent triple (the model goes through SAM JSON, as the workflow saves it).</summary>
        /// <param name="designDays">False: the TBD holds no design-day group, the TSD no data set and SAM no design-day peak (a justified absence).</param>
        /// <param name="heatingTimeSignal">False: the heating design day's condition has no gains, as in the S08 PR-C fixtures.</param>
        /// <param name="heatingFullDay">True: each zone's heating design day is a 24-way tie (as on the acc1 box).</param>
        public static ResultInputs Inputs(bool designDays = true, bool heatingTimeSignal = true, bool heatingFullDay = false)
        {
            List<Zone> zones = Zones(heatingFullDay);
            AnalyticalModel model = Model(zones, designDays);
            AnalyticalModelSimulationResult modelResult = ModelResult(zones);
            return new ResultInputs
            {
                TsdJson = JsonSerializer.Serialize(Tsd(zones, designDays), SnapshotJson.Options),
                TbdJson = JsonSerializer.Serialize(Tbd(zones, heatingTimeSignal), SnapshotJson.Options),
                CoverageJson = JsonSerializer.Serialize(Coverage(designDays), SnapshotJson.Options),
                Rules = CalibrationRules.Load(Evidence.Rules),
                ModelJson = Core.Convert.ToString(model),
                B1aJson = B1a(model, modelResult),
                ModelResultJson = JsonSerializer.Serialize(ResultSubject.ModelResult(modelResult), SnapshotJson.Options),
            };
        }
    }
}
