// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests. It touches no TAS type and no
// SAM_Tas code under test (enforced by ResultSourceScanTests): every expected value is computed here from the raw arrays.
namespace SAM.Analytical.Tas.DirectT3D.Validation.Results
{
    /// <summary>What a reference row expects.</summary>
    public enum ExpectedState
    {
        /// <summary>A value (or, with <see cref="ReferenceRow.Allowed"/>, one of a set of values; or a text).</summary>
        Value,

        /// <summary>No value: the subject must hold nothing (an unavailable metric, an absent peak, a peak with no hour).</summary>
        NoValue,

        /// <summary>The reference cannot be established; the row is BLOCKED whatever the subject holds.</summary>
        Blocked
    }

    /// <summary>One required comparison, with the reference expectation and how it was derived.</summary>
    public sealed class ReferenceRow
    {
        /// <summary>Unique: layer | scope | load type | quantity.</summary>
        public string Id { get; set; }

        /// <summary>SAM (the SAM-imported results), B1a (the benchmark document) or Identity.</summary>
        public string Layer { get; set; }

        /// <summary>"model" or the TSD zone GUID.</summary>
        public string Scope { get; set; }
        public string ZoneName { get; set; }
        public string LoadType { get; set; }
        public string Quantity { get; set; }

        /// <summary>R0, R1, R2 (S10 OD-3), Hour (exact, with the tie set), Text or Count.</summary>
        public string Class { get; set; }
        public string Unit { get; set; }
        public ExpectedState State { get; set; }
        public double Value { get; set; }

        /// <summary>For an hour: every tied maximum, any of which is accepted (S10 tie rule). Null = only <see cref="Value"/>.</summary>
        public List<double> Allowed { get; set; }
        public string Text { get; set; }

        /// <summary>The derivation, or why the row is blocked.</summary>
        public string Reason { get; set; }
    }

    /// <summary>One native convention the reference depends on, measured on the run's own evidence.</summary>
    public sealed class CalibrationCheck
    {
        public string Id { get; set; }
        public string Question { get; set; }

        /// <summary>verified, unresolved or not-exercised. Rows that depend on an unresolved check are BLOCKED.</summary>
        public string Status { get; set; }
        public string Answer { get; set; }
        public List<string> Evidence { get; set; } = new List<string>();
    }

    public sealed class ResultReferenceModel
    {
        public const string CurrentSchema = "s10-result-reference/1";

        public string Schema { get; set; } = CurrentSchema;
        public string TsdPath { get; set; }
        public string TsdSha256 { get; set; }
        public string TbdPath { get; set; }
        public string TbdSha256 { get; set; }
        public string CoverageSha256 { get; set; }
        public string RulesSha256 { get; set; }

        /// <summary>Problems that block every row (reader errors, a part-year TSD, unverified alignment).</summary>
        public List<string> Blockers { get; set; } = new List<string>();
        public List<CalibrationCheck> Checks { get; set; } = new List<CalibrationCheck>();
        public List<ReferenceRow> Rows { get; set; } = new List<ReferenceRow>();

        /// <summary>Reported, never gated: tie counts, the governing basis, the native GetPeakZoneGains cross-check.</summary>
        public List<string> Observations { get; set; } = new List<string>();
    }

    /// <summary>
    /// The S10 reference: the values the SAM-imported results and the B1a document must hold, computed independently from
    /// the raw TSD arrays (<see cref="TsdResultSnapshot"/>) and the raw TBD read (<see cref="NativeSnapshot"/>), under the
    /// committed N-5/N-6 calibration rules. Fail closed: an array that is missing, unread in any element, outside the
    /// calibrated domain, non-finite or negative never yields a value, and every row that depends on it is BLOCKED.
    /// <para>Units, signs and interval (S10 amendment): Tas results are hourly sensible powers in W (Tas Theory Manual,
    /// "Simulation Principles" and "Plant Operation: Sensible Load", Documentation/TAS_Theory.pdf; S08 P-ALIGN: an authored
    /// 10 W/m2 x 20 m2 lighting gain reads back as 200 and a one-hour equipment pulse sums to 200, replayed offline). Per run,
    /// an annual array holds 24 values per simulated day (1 h each); the plant loads are non-negative magnitudes; and the
    /// zone's sensible load breakdown, which the manual defines as a balanced account, closes as heating - cooling + the
    /// breakdown terms = 0, which ties the load arrays to the breakdown's W and fixes heating as plant input and cooling as
    /// its negative. The building profiles must equal the sum of the zone loads. Conversions: zone loads W unchanged;
    /// model consumption kWh = sum(W x 1 h) / 1000; model peak kW = max / 1000; hour = CLR index - 1 (N-5).</para>
    /// <para>Design days (S10 revision-1 review R1/R2): the TBD design-day groups, read independently and fail closed, are
    /// the expectation of the TSD design-day data sets (S10-C10); their series' index and day origin must be established
    /// from Tas's own peak index and the TBD gain schedule (S10-C9). Unresolved coverage blocks the role's design-day and
    /// legacy rows; an unresolved convention blocks only the rows that depend on it (hours, design-governed indices).</para>
    /// <para>Design-day representation (S10 revision 2, measured on the acc1 s08-fix-box TSD, both roles): a data set's
    /// series are calendar-shaped, 8760 elements at the N-5 lower bound, populated only at the 24 CLR indices
    /// (d - 1) x 24 + 1 .. d x 24 of the TBD design day's own year day d (read independently from the TBD, never inferred
    /// from the series) and holding Tas's -1 everywhere else. The expected mask comes from the TBD; every element inside it
    /// must be read and finite (loads non-negative), every element outside it must be -1, and the raw positions and CLR
    /// indices are kept (never compacted or re-based). Only this single-design-day layout was measured; a group of several
    /// design days, a shared year day or any other length stays BLOCKED (S10-C7).</para>
    /// </summary>
    public static class ResultReference
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public const string TasSource = "SAM.Analytical.Tas";
        public const string Heating = "Heating";
        public const string Cooling = "Cooling";
        public const int HoursPerDay = 24;
        public const int AnnualHours = 8760;

        /// <summary>The Tas sensible load breakdown terms (Theory Manual, "Sensible Load Breakdown"), plus the AHU gain.</summary>
        public static readonly string[] BalanceTerms =
        {
            "solarGain", "lightingGain", "occupantSensibleGain", "equipmentSensibleGain", "infVentGain", "airMovementGain",
            "buildingHeatTransfer", "externalConductionOpaque", "externalConductionGlazing", "AHUGain",
        };

        /// <summary>Balance closure: |heating - cooling + sum(terms)| at an hour within this share of sum(|terms|) ...</summary>
        public const double BalanceRelative = 0.01;

        /// <summary>... plus this many W.</summary>
        public const double BalanceAbsolute = 0.5;

        /// <summary>Building profile = sum of zone loads, within this share of sum(|zone loads|) plus <see cref="BuildingAbsolute"/> W (float32 storage).</summary>
        public const double BuildingRelative = 1e-6;
        public const double BuildingAbsolute = 1e-6;

        /// <summary>The unmet-hour margin SAM states (heating unmet when T + margin &lt; LL, cooling when T - margin &gt; UL).</summary>
        public const double UnmetMargin = 0.5;

        /// <summary>The legacy Tas value SAM's governing projection stores for a zero peak (index 0; documented, Results.cs).</summary>
        public const double LegacyZeroSentinel = -1;

        /// <summary>What Tas holds at every element of a calendar-shaped design-day series outside the design day (measured, S10 revision 2).</summary>
        public const double DesignOutsideSentinel = -1;

        /// <param name="coverage">The independent TBD design-day coverage read (<c>results-read-design</c>); null blocks every design-day and legacy row.</param>
        public static ResultReferenceModel Build(TsdResultSnapshot tsd, NativeSnapshot tbd, TbdDesignCoverage coverage, CalibrationRules rules)
        {
            ResultReferenceModel result = new ResultReferenceModel
            {
                TsdPath = tsd?.Path,
                TsdSha256 = tsd?.Sha256After,
                TbdPath = tbd?.Reader?.TbdPath,
                TbdSha256 = tbd?.Reader?.TbdSha256After,
                CoverageSha256 = coverage?.Sha256After,
                RulesSha256 = rules?.SourceSha256,
            };

            Builder builder = new Builder(result, tsd, tbd, coverage, rules ?? CalibrationRules.None());
            builder.Run();
            return result;
        }

        /// <summary>Every day offset o at which gains[p] = k x schedule[(p - o) mod 24] for all p (k = max ratio; float32 tolerance).</summary>
        public static List<int> Offsets(double[] gains, double[] schedule)
        {
            List<int> result = new List<int>();
            double gainMax = gains.Max(), scheduleMax = schedule.Max();
            if (gainMax <= 0 || scheduleMax <= 0) return result;
            double k = gainMax / scheduleMax, tolerance = 1e-5 * gainMax + 1e-6;
            for (int o = 0; o < HoursPerDay; o++)
            {
                bool fit = true;
                for (int p = 0; p < gains.Length && fit; p++)
                {
                    fit = Math.Abs(gains[p] - k * schedule[((p - o) % HoursPerDay + HoursPerDay) % HoursPerDay]) <= tolerance;
                }

                if (fit) result.Add(o);
            }

            return result;
        }

        // ---- pure arithmetic, public for the offline tests -------------------------------------------------------------

        /// <summary>The maximum and every position holding it, of a validated non-negative series.</summary>
        public static (double max, List<int> ties) Peak(double[] values)
        {
            double max = double.NegativeInfinity;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] > max) max = values[i];
            }

            List<int> ties = new List<int>();
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == max) ties.Add(i);
            }

            return (max, ties);
        }

        /// <summary>
        /// The maximum over the 24 elements of the design day starting at position <paramref name="start"/>, and every
        /// position holding it, as positions of the whole series (CLR index = position + lower bound).
        /// </summary>
        public static (double max, List<int> ties) DayPeak(double[] values, int start)
        {
            (double max, List<int> ties) = Peak(Day(values, start));
            return (max, ties.Select(x => x + start).ToList());
        }

        /// <summary>The 24 elements of the design day starting at position <paramref name="start"/>.</summary>
        public static double[] Day(double[] values, int start)
        {
            double[] result = new double[HoursPerDay];
            Array.Copy(values, start, result, 0, HoursPerDay);
            return result;
        }

        /// <summary>The first position of a calendar-shaped design-day series holding year day <paramref name="yearDay"/> (1..365).</summary>
        public static int DayStart(int yearDay) => (yearDay - 1) * HoursPerDay;

        /// <summary>
        /// Why <paramref name="values"/> (validated complete and finite) is not populated exactly at the design day starting at
        /// <paramref name="start"/>, or null: every element outside the day must be Tas's -1, and a load inside it a
        /// non-negative magnitude (so never the sentinel).
        /// </summary>
        public static string DayMask(double[] values, int start, int lowerBound, bool load, string what)
        {
            int outside = 0, firstOutside = -1, negative = 0, firstNegative = -1;
            for (int p = 0; p < values.Length; p++)
            {
                if (p >= start && p < start + HoursPerDay)
                {
                    if (load && values[p] < 0 && negative++ == 0) firstNegative = p;
                }
                else if (values[p] != DesignOutsideSentinel && outside++ == 0)
                {
                    firstOutside = p;
                }
            }

            string day = "CLR " + (start + lowerBound).ToString(ci) + ".." + (start + HoursPerDay - 1 + lowerBound).ToString(ci);
            if (outside > 0)
            {
                return what + " holds " + outside.ToString(ci) + " element(s) other than Tas's -1 outside the design day " + day + ", first " + Num(values[firstOutside]) + " at CLR " + (firstOutside + lowerBound).ToString(ci) + ": the populated hours are not the TBD year day's";
            }

            if (negative > 0)
            {
                return what + " holds " + negative.ToString(ci) + " negative element(s) inside the design day " + day + ", first " + Num(values[firstNegative]) + " at CLR " + (firstNegative + lowerBound).ToString(ci) + ": a load there is a non-negative magnitude (Tas's -1 marks only hours outside the day)";
            }

            return null;
        }

        /// <summary>
        /// Why <paramref name="series"/> is not a complete annual array of finite numbers at the calibrated lower bound, or
        /// null with the values (position = 0-based hour).
        /// </summary>
        public static double[] Annual(ResultSeries series, int lowerBound, int length, string what, out string problem)
        {
            problem = Shape(series, what);
            if (problem != null) return null;

            if (series.LowerBound != lowerBound || series.Values.Length != length)
            {
                problem = what + " has " + series.Values.Length.ToString(ci) + " elements at lower bound " + series.LowerBound.ToString(ci) + "; expected " + length.ToString(ci) + " at lower bound " + lowerBound.ToString(ci) + " (N-5, 24 per simulated day): it is never truncated, padded or shifted";
                return null;
            }

            return series.Values;
        }

        /// <summary>Why the series was not read completely as finite numbers, or null.</summary>
        public static string Shape(ResultSeries series, string what)
        {
            if (series == null)
            {
                return what + " was not read";
            }

            if (!series.IsRead)
            {
                return what + " is " + series.Kind + (series.Detail == null ? string.Empty : " (" + series.Detail + ")");
            }

            if (series.UnreadElements != null && series.UnreadElements.Count > 0)
            {
                return what + " has " + series.UnreadElements.Count.ToString(ci) + " unread element(s), first at CLR index " + series.UnreadElements[0].ToString(ci) + ": a missing value is never a zero";
            }

            if (series.Values.Length == 0)
            {
                return what + " is empty";
            }

            int bad = series.Values.Count(x => double.IsNaN(x) || double.IsInfinity(x));
            if (bad > 0)
            {
                int first = Array.FindIndex(series.Values, x => double.IsNaN(x) || double.IsInfinity(x));
                return what + " has " + bad.ToString(ci) + " NaN/infinite element(s), first at CLR index " + (series.LowerBound + first).ToString(ci);
            }

            return null;
        }

        /// <summary>Why a load series is not a non-negative magnitude, or null.</summary>
        public static string Sign(double[] values, string what)
        {
            int negative = values.Count(x => x < 0);
            if (negative == 0) return null;

            int first = Array.FindIndex(values, x => x < 0);
            return what + " has " + negative.ToString(ci) + " negative element(s), first " + values[first].ToString("R", ci) + " at position " + first.ToString(ci) + ": a Tas plant load array is a non-negative magnitude, so the sign is unresolved";
        }

        /// <summary>
        /// Unmet hours as SAM states them (heating unmet when T + margin &lt; LL; cooling when T - margin &gt; UL), counted over
        /// the 8760 hours.
        /// </summary>
        public static int UnmetHours(double[] dryBulb, double[] setpoint, bool heating)
        {
            int count = 0;
            for (int h = 0; h < dryBulb.Length; h++)
            {
                if (heating ? dryBulb[h] + UnmetMargin < setpoint[h] : dryBulb[h] - UnmetMargin > setpoint[h]) count++;
            }

            return count;
        }

        public static bool SameGuid(string a, string b)
        {
            return a != null && b != null && Guid.TryParse(a, out Guid x) && Guid.TryParse(b, out Guid y) && x == y;
        }

        public static string Num(double value)
        {
            return value.ToString("R", ci);
        }

        // ---- the builder ------------------------------------------------------------------------------------------------

        private sealed class ZoneSeries
        {
            public TsdResultZone Zone;
            public Dictionary<string, double[]> Values = new Dictionary<string, double[]>(StringComparer.Ordinal);
            public Dictionary<string, string> Problems = new Dictionary<string, string>(StringComparer.Ordinal);
            public string BalanceProblem;

            public double[] Get(string name, out string problem)
            {
                Values.TryGetValue(name, out double[] values);
                Problems.TryGetValue(name, out problem);
                return problem == null ? values : null;
            }
        }

        private sealed class DesignPeak
        {
            public bool Present;
            public string Problem;
            public double Max;
            public List<int> Ties = new List<int>();
            public int LowerBound;
            public string Name;
            public int DatasetsTied;
        }

        private sealed class Builder
        {
            private readonly ResultReferenceModel model;
            private readonly TsdResultSnapshot tsd;
            private readonly NativeSnapshot tbd;
            private readonly CalibrationRules rules;
            private readonly TbdDesignCoverage coverage;
            private readonly List<ZoneSeries> zones = new List<ZoneSeries>();
            private string global;
            private int offset = 1;
            private string identityProblem;

            // Per role (Cooling/Heating): why its design-day data sets are unusable (coverage or shape), why their native index
            // is not established, why their hour of day is not; and the TBD group each TSD data set corresponds to.
            private readonly Dictionary<string, string> roleProblems = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> indexProblems = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> originProblems = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, DesignCoverageGroup> datasetGroups = new Dictionary<string, DesignCoverageGroup>(StringComparer.Ordinal);

            // Per data set (role#index) with a verified calendar shape (S10-C7): the first position of its design day.
            private readonly Dictionary<string, int> datasetStarts = new Dictionary<string, int>(StringComparer.Ordinal);

            public Builder(ResultReferenceModel model, TsdResultSnapshot tsd, NativeSnapshot tbd, TbdDesignCoverage coverage, CalibrationRules rules)
            {
                this.model = model;
                this.tsd = tsd;
                this.tbd = tbd;
                this.coverage = coverage;
                this.rules = rules;
            }

            public void Run()
            {
                CheckRun();
                CheckIdentity();
                ReadZones();
                CheckBalance();
                CheckDesignCoverage();
                CheckDesignShapes();
                CheckDesignConvention();

                ModelRows();
                IdentityRows();
                foreach (ZoneSeries zone in zones)
                {
                    ZoneRows(zone, Heating);
                    ZoneRows(zone, Cooling);
                }

                if (identityProblem != null)
                {
                    // Without a unique zone identity no zone row can be attributed to a SAM space or a B1a space.
                    foreach (ReferenceRow row in model.Rows.Where(x => x.Scope != "model"))
                    {
                        Block(row, "zone identity unresolved: " + identityProblem);
                    }
                }

                if (global != null)
                {
                    foreach (ReferenceRow row in model.Rows)
                    {
                        Block(row, global);
                    }
                }
            }

            // -- checks ----------------------------------------------------------------------------------------------

            private void CheckRun()
            {
                List<string> blockers = model.Blockers;
                if (tsd == null) blockers.Add("no TSD result snapshot");
                else blockers.AddRange(tsd.Errors.Select(x => "TSD read: " + x));
                if (tbd == null) blockers.Add("no TBD snapshot");
                else blockers.AddRange(tbd.Errors.Select(x => "TBD read: " + x));

                CalibrationCheck window = Check("S10-C1", "Is the TSD a full-year simulation (days 1..365)?");
                if (tsd != null)
                {
                    double first = tsd.FirstDay?.HasNumber == true ? tsd.FirstDay.Value : double.NaN;
                    double last = tsd.LastDay?.HasNumber == true ? tsd.LastDay.Value : double.NaN;
                    window.Evidence.Add("firstDay " + (tsd.FirstDay?.ToString() ?? "not read") + ", lastDay " + (tsd.LastDay?.ToString() ?? "not read"));
                    if (first == 1 && last == 365)
                    {
                        Verify(window, "days 1..365");
                    }
                    else
                    {
                        Unresolve(window, "not a full year: S10 refuses a part-year or unread simulation window");
                        blockers.Add("the TSD simulation window is not days 1..365 (" + (tsd.FirstDay?.ToString() ?? "?") + ".." + (tsd.LastDay?.ToString() ?? "?") + "): a part-year TSD is refused");
                    }
                }
                else
                {
                    Unresolve(window, "no TSD");
                }

                CalibrationCheck alignment = Check("S10-C2", "Which CLR index of a TSD annual array holds SAM hour h (N-5)?");
                int weather = rules.Offset(CalibrationRules.WeatherIndex, 1, out bool weatherVerified);
                alignment.Evidence.Add("calibration-rules.json " + (rules.SourceSha256 ?? "(no table)") + ": " + CalibrationRules.Alignment + " " + (rules.IsVerified(CalibrationRules.Alignment) ? "verified" : "not verified") + ", " + CalibrationRules.WeatherIndex + " " + (weatherVerified ? "verified offset " + weather.ToString(ci) : "not verified"));
                alignment.Evidence.Add("S08 P-ALIGN TSD: externalTemperature lower bound 1, length 8760, pulses aligned with weather shift 0");
                if (rules.IsVerified(CalibrationRules.Alignment) && weatherVerified)
                {
                    offset = weather;
                    Verify(alignment, "CLR index = h + " + offset.ToString(ci) + " (lower bound " + offset.ToString(ci) + ", 8760 elements)");
                }
                else
                {
                    Unresolve(alignment, "N-5 alignment not verified in the rule table");
                    blockers.Add("the N-5 TSD alignment rule is not verified: no hour can be attributed");
                }

                if (blockers.Count > 0)
                {
                    global = string.Join("; ", blockers);
                }
            }

            private void CheckIdentity()
            {
                CalibrationCheck check = Check("S10-C8", "Are the TSD zones uniquely identified (GUID and name) and matched to TBD zones?");
                List<TsdResultZone> tsdZones = tsd?.Zones ?? new List<TsdResultZone>();
                List<string> problems = new List<string>();
                if (tsdZones.Count == 0) problems.Add("the TSD has no zones");

                foreach (IGrouping<string, TsdResultZone> group in tsdZones.GroupBy(x => x.Name ?? string.Empty, StringComparer.Ordinal))
                {
                    if (string.IsNullOrEmpty(group.Key)) problems.Add(group.Count() + " zone(s) without a name");
                    else if (group.Count() > 1) problems.Add("zone name '" + group.Key + "' is not unique (" + group.Count() + " zones): identity by name is refused");
                }

                foreach (TsdResultZone zone in tsdZones)
                {
                    if (!Guid.TryParse(zone.Guid, out _)) problems.Add("zone '" + zone.Name + "' has no readable GUID ('" + zone.Guid + "')");
                    else if (tsdZones.Count(x => SameGuid(x.Guid, zone.Guid)) != 1) problems.Add("zone GUID " + zone.Guid + " is not unique");
                    int tbdMatches = tbd?.Zones?.Count(x => SameGuid(x.Guid, zone.Guid)) ?? 0;
                    check.Evidence.Add("TSD zone " + zone.Index + " '" + zone.Name + "' " + zone.Guid + ": " + tbdMatches + " TBD zone(s) with that GUID");
                }

                if (problems.Count == 0)
                {
                    Verify(check, tsdZones.Count + " zone(s), unique names and GUIDs");
                }
                else
                {
                    identityProblem = string.Join("; ", problems);
                    Unresolve(check, identityProblem);
                }
            }

            private void ReadZones()
            {
                CalibrationCheck domain = Check("S10-C3", "Are the annual arrays complete hourly series (24 finite values per simulated day, 1 h each) at the calibrated lower bound?");
                CalibrationCheck sign = Check("S10-C4", "Are the plant load arrays non-negative magnitudes?");
                List<string> domainProblems = new List<string>(), signProblems = new List<string>();

                foreach (TsdResultZone zone in tsd?.Zones ?? new List<TsdResultZone>())
                {
                    ZoneSeries item = new ZoneSeries { Zone = zone };
                    foreach (string name in new[] { "heatingLoad", "coolingLoad", "dryBulbTemp" }.Concat(BalanceTerms))
                    {
                        zone.Arrays.TryGetValue(name, out ResultSeries series);
                        double[] values = Annual(series, offset, AnnualHours, "zone '" + zone.Name + "' " + name, out string problem);
                        if (problem == null && (name == "heatingLoad" || name == "coolingLoad"))
                        {
                            problem = Sign(values, "zone '" + zone.Name + "' " + name);
                            if (problem != null) signProblems.Add(problem);
                        }
                        else if (problem != null)
                        {
                            domainProblems.Add(problem);
                        }

                        item.Values[name] = values;
                        if (problem != null) item.Problems[name] = problem;
                    }

                    zones.Add(item);
                }

                foreach (string name in new[] { "heatingProfile", "coolingProfile" })
                {
                    ResultSeries series = null;
                    tsd?.Building?.TryGetValue(name, out series);
                    double[] values = Annual(series, offset, AnnualHours, "building " + name, out string problem);
                    if (problem == null)
                    {
                        problem = Sign(values, "building " + name);
                        if (problem != null) signProblems.Add(problem);
                    }
                    else
                    {
                        domainProblems.Add(problem);
                    }

                    buildingSeries[name] = values;
                    if (problem != null) buildingProblems[name] = problem;
                }

                domain.Evidence.Add("expected per array: " + AnnualHours + " = 24 x 365 elements at lower bound " + offset + ", every element a finite number");
                domain.Evidence.Add("Tas Theory Manual: the simulation is a series of hourly snapshots, each array element one hour");
                if (domainProblems.Count == 0) Verify(domain, "hourly, 1 h per element, 8760 elements"); else Unresolve(domain, string.Join("; ", domainProblems.Take(20)));
                if (signProblems.Count == 0) Verify(sign, "heatingLoad, coolingLoad, heatingProfile and coolingProfile are >= 0 everywhere"); else Unresolve(sign, string.Join("; ", signProblems.Take(20)));
            }

            private readonly Dictionary<string, double[]> buildingSeries = new Dictionary<string, double[]>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> buildingProblems = new Dictionary<string, string>(StringComparer.Ordinal);

            private void CheckBalance()
            {
                CalibrationCheck balance = Check("S10-C5", "Do the load arrays close the zone's sensible load breakdown as heating - cooling + terms = 0 (W, heating = plant input, cooling = its negative)?");
                balance.Evidence.Add("Tas Theory Manual, Sensible Load Breakdown: a balanced account of the zone's sensible heat flows in W, plant input positive for heating and negative for cooling; tolerance per hour " + Num(BalanceRelative) + " x sum(|terms|) + " + Num(BalanceAbsolute) + " W; terms " + string.Join(", ", BalanceTerms));
                List<string> problems = new List<string>();
                foreach (ZoneSeries zone in zones)
                {
                    string missing = new[] { "heatingLoad", "coolingLoad" }.Concat(BalanceTerms).Select(x => { zone.Get(x, out string p); return p; }).FirstOrDefault(x => x != null);
                    if (missing != null)
                    {
                        zone.BalanceProblem = "the load breakdown cannot be closed: " + missing;
                        problems.Add(zone.BalanceProblem);
                        continue;
                    }

                    double[] heating = zone.Values["heatingLoad"], cooling = zone.Values["coolingLoad"];
                    int violations = 0, worst = -1, swappedViolations = 0;
                    double worstResidual = 0, worstScale = 0;
                    for (int h = 0; h < AnnualHours; h++)
                    {
                        double terms = 0, scale = Math.Abs(heating[h]) + Math.Abs(cooling[h]);
                        foreach (string term in BalanceTerms)
                        {
                            terms += zone.Values[term][h];
                            scale += Math.Abs(zone.Values[term][h]);
                        }

                        double residual = heating[h] - cooling[h] + terms;
                        double tolerance = BalanceRelative * scale + BalanceAbsolute;
                        if (Math.Abs(residual) > tolerance) violations++;
                        if (Math.Abs(cooling[h] - heating[h] + terms) > tolerance) swappedViolations++;
                        if (Math.Abs(residual) > Math.Abs(worstResidual))
                        {
                            worstResidual = residual;
                            worst = h;
                            worstScale = scale;
                        }
                    }

                    double ahu = zone.Values["AHUGain"].Max(x => Math.Abs(x));
                    string evidence = "zone '" + zone.Zone.Name + "': " + violations + " hour(s) outside tolerance; largest residual " + Num(worstResidual) + " W at hour " + worst + " (sum |terms| " + Num(worstScale) + " W); hours with heating > 0: " + heating.Count(x => x > 0) + ", cooling > 0: " + cooling.Count(x => x > 0) + ", both: " + Enumerable.Range(0, AnnualHours).Count(h => heating[h] > 0 && cooling[h] > 0) + "; roles swapped would violate " + swappedViolations + " hour(s); max |AHUGain| " + Num(ahu) + " W";
                    balance.Evidence.Add(evidence);
                    if (violations > 0)
                    {
                        zone.BalanceProblem = "the load units/roles/sign are unresolved: " + evidence;
                        problems.Add(zone.BalanceProblem);
                    }
                }

                if (zones.Count == 0) Unresolve(balance, "no zones");
                else if (problems.Count == 0) Verify(balance, "closes in every zone and hour: the loads are W, heating is plant input and cooling its negative");
                else Unresolve(balance, string.Join("; ", problems));

                CalibrationCheck building = Check("S10-C6", "Is the building heating/cooling profile the sum of the zone loads (so it is in W)?");
                foreach (string pair in new[] { "heatingProfile:heatingLoad", "coolingProfile:coolingLoad" })
                {
                    string profile = pair.Split(':')[0], load = pair.Split(':')[1];
                    if (buildingProblems.TryGetValue(profile, out string problem))
                    {
                        Unresolve(building, problem);
                        continue;
                    }

                    string zoneProblem = zones.Select(x => { x.Get(load, out string p); return p; }).FirstOrDefault(x => x != null);
                    if (zoneProblem != null || zones.Count == 0)
                    {
                        buildingProblems[profile] = "building " + profile + " cannot be tied to the zone loads: " + (zoneProblem ?? "no zones");
                        Unresolve(building, buildingProblems[profile]);
                        continue;
                    }

                    double[] values = buildingSeries[profile];
                    int violations = 0;
                    double worst = 0;
                    for (int h = 0; h < AnnualHours; h++)
                    {
                        double sum = 0, scale = 0;
                        foreach (ZoneSeries zone in zones)
                        {
                            sum += zone.Values[load][h];
                            scale += Math.Abs(zone.Values[load][h]);
                        }

                        double difference = values[h] - sum;
                        if (Math.Abs(difference) > BuildingRelative * scale + BuildingAbsolute) violations++;
                        if (Math.Abs(difference) > Math.Abs(worst)) worst = difference;
                    }

                    building.Evidence.Add("building " + profile + " vs sum of zone " + load + ": " + violations + " hour(s) outside " + Num(BuildingRelative) + " x sum + " + Num(BuildingAbsolute) + " W; largest difference " + Num(worst) + " W");
                    if (violations > 0)
                    {
                        buildingProblems[profile] = "building " + profile + " is not the sum of the zone " + load + " (" + violations + " hour(s)): its unit is unresolved";
                        Unresolve(building, buildingProblems[profile]);
                    }
                }

                if (building.Status == null) Verify(building, "both profiles equal the sum of the zone loads: W");
            }

            // -- design days (S10 R1/R2) ---------------------------------------------------------------------------------

            private static readonly string[] Roles = { Cooling, Heating };

            private List<TsdDesignDataset> Datasets(string role)
            {
                return (tsd?.DesignDatasets ?? new List<TsdDesignDataset>()).Where(x => x.Role == role).OrderBy(x => x.Index).ToList();
            }

            /// <summary>
            /// S10-C10 (R2): the TBD design-day groups are the independent expectation. Every group must have exactly one TSD data
            /// set of its role and exact name, and no TSD data set may be unexpected; absence is accepted only when the coverage
            /// was read completely and holds no group of the role. Anything else blocks the role's design-day and legacy rows,
            /// whatever the subject holds.
            /// </summary>
            private void CheckDesignCoverage()
            {
                CalibrationCheck check = Check("S10-C10", "Does every TBD design-day group have exactly one TSD design-day data set of the same role and exact name, with no other data set (absence accepted only when the completely read TBD holds no group)?");
                foreach (string role in Roles)
                {
                    List<TsdDesignDataset> datasets = Datasets(role);
                    List<DesignCoverageGroup> groups = null;
                    string problem = null;
                    if (coverage == null) problem = "no TBD design-day coverage was read (tbd-design-coverage.json): the expected design-day data sets are unknown";
                    else if (coverage.Errors.Count > 0) problem = "the TBD design-day coverage is unreadable: " + string.Join("; ", coverage.Errors);
                    else
                    {
                        groups = coverage.Groups.Where(x => x.Role == role).ToList();
                        string unreadable = groups.Select(x => x.Detail != null ? "group " + x.Index + " ('" + x.Name + "'): " + x.Detail : x.Name == null ? "group " + x.Index + " has no name" : null).FirstOrDefault(x => x != null);
                        if (unreadable != null) problem = "the TBD " + role + " design-day coverage is unreadable: " + unreadable;
                    }

                    if (problem == null)
                    {
                        List<string> found = new List<string>();
                        List<string> duplicateGroups = groups.GroupBy(x => x.Name, StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => "'" + x.Key + "' x" + x.Count()).ToList();
                        if (duplicateGroups.Count > 0) found.Add("TBD groups not unique: " + string.Join(", ", duplicateGroups));
                        List<string> unnamed = datasets.Where(x => x.Name == null).Select(x => x.Index.ToString(ci)).ToList();
                        if (unnamed.Count > 0) found.Add("TSD data set(s) without a readable name: " + string.Join(", ", unnamed));
                        List<string> duplicates = datasets.Where(x => x.Name != null).GroupBy(x => x.Name, StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => "'" + x.Key + "' x" + x.Count()).ToList();
                        if (duplicates.Count > 0) found.Add("duplicate TSD data sets: " + string.Join(", ", duplicates));
                        List<string> missing = groups.Where(g => !datasets.Any(d => string.Equals(d.Name, g.Name, StringComparison.Ordinal))).Select(g => "'" + g.Name + "'").ToList();
                        if (missing.Count > 0) found.Add("expected but missing in the TSD: " + string.Join(", ", missing));
                        List<string> extra = datasets.Where(d => d.Name != null && !groups.Any(g => string.Equals(g.Name, d.Name, StringComparison.Ordinal))).Select(d => "'" + d.Name + "'").ToList();
                        if (extra.Count > 0) found.Add("in the TSD but not a TBD group: " + string.Join(", ", extra));
                        if (found.Count > 0) problem = role + " design-day coverage: " + string.Join("; ", found);
                    }

                    check.Evidence.Add(role + ": TBD groups [" + (groups == null ? "unread" : string.Join(", ", groups.Select(x => "'" + x.Name + "' (" + x.Days.Count + " day(s))"))) + "]; TSD data sets [" + string.Join(", ", datasets.Select(x => "'" + x.Name + "'")) + "]");
                    if (problem != null)
                    {
                        roleProblems[role] = problem;
                        Unresolve(check, problem);
                        continue;
                    }

                    foreach (TsdDesignDataset dataset in datasets)
                    {
                        datasetGroups[role + "#" + dataset.Index] = groups.Single(g => string.Equals(g.Name, dataset.Name, StringComparison.Ordinal));
                    }

                    if (groups.Count == 0) check.Evidence.Add(role + ": the completely read TBD holds no design-day group and the TSD no data set: no design-day peak is expected");
                }

                Verify(check, "the TSD design-day data sets correspond one to one, by role and exact name, to the TBD design-day groups");
            }

            /// <summary>
            /// S10-C7: every matched design-day data set is in the measured calendar representation. Its TBD group holds one
            /// design day whose year day d (read from the TBD, 1..365) gives the expected mask; every series the reference uses
            /// is 8760 elements at the N-5 lower bound, complete and finite, a load inside the mask non-negative, and every
            /// element outside the mask Tas's -1. The mask is never discovered from the series. Any other layout, including the
            /// earlier assumed 24 or 24 x repetitions elements, a group of several days and two data sets sharing a year day,
            /// was not measured and blocks the role's design-day and legacy rows.
            /// </summary>
            private void CheckDesignShapes()
            {
                CalibrationCheck check = Check("S10-C7", "Is every design-day data set in the measured calendar representation (one TBD design day of year day d; every series 8760 elements at the N-5 lower bound; complete finite values, loads non-negative, at CLR (d - 1) x 24 + 1 .. d x 24; Tas's -1 at every other element)?");
                check.Evidence.Add("representation measured on the S10 revision-2 acc1 s08-fix-box TSD (both roles, five series each): populated exactly at the TBD yearDay's 24 CLR indices, -1 elsewhere; the year day is the TBD design day's yearDay (raw TBD read, tbd-snapshot.json), matched to the coverage group by role and exact name");
                int exercised = 0;
                Dictionary<string, int> yearDays = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string role in Roles)
                {
                    if (roleProblems.ContainsKey(role)) continue;
                    string load = role == Cooling ? "coolingLoad" : "heatingLoad";
                    foreach (TsdDesignDataset dataset in Datasets(role))
                    {
                        exercised++;
                        string key = role + "#" + dataset.Index;
                        string label = role + " design-day data set " + dataset.Index + " '" + dataset.Name + "'";
                        DesignCoverageGroup group = datasetGroups[key];
                        string problem = null;
                        int yearDay = 0;
                        string day = group.Days.Select(x => x.Detail != null ? "design day " + x.Index + ": " + x.Detail : null).FirstOrDefault(x => x != null);
                        if (day != null) problem = label + ": its TBD group is not completely readable (" + day + ")";
                        else yearDay = YearDay(role, label, group, out problem);

                        if (problem == null && dataset.Zones.Count != (tsd?.Zones?.Count ?? 0))
                        {
                            problem = label + " has " + dataset.Zones.Count + " zone(s); the building data has " + (tsd?.Zones?.Count ?? 0);
                        }

                        int start = DayStart(yearDay);
                        int arrays = 0;
                        foreach (TsdResultZone zone in problem == null ? dataset.Zones : new List<TsdResultZone>())
                        {
                            problem = CalendarSeries(zone, load, start, label, ref arrays);
                            if (problem != null) break;
                        }

                        check.Evidence.Add(label + ": " + (problem ?? "TBD yearDay " + yearDay + " -> CLR " + (start + offset) + ".." + (start + HoursPerDay - 1 + offset) + "; " + arrays + " series in " + dataset.Zones.Count + " zone(s), each 8760 at lower bound " + offset + " with 24 values inside the day and -1 at the other 8736"));
                        if (problem != null)
                        {
                            if (!roleProblems.ContainsKey(role)) roleProblems[role] = problem;
                            Unresolve(check, problem);
                            continue;
                        }

                        datasetStarts[key] = start;
                        yearDays[key] = yearDay;
                    }
                }

                // Two data sets on one year day (an overlap) were not measured.
                foreach (IGrouping<int, KeyValuePair<string, int>> shared in yearDays.GroupBy(x => x.Value).Where(x => x.Count() > 1))
                {
                    string problem = "design-day data sets " + string.Join(", ", shared.Select(x => x.Key)) + " share year day " + shared.Key + ": overlapping design days were not measured";
                    foreach (string role in shared.Select(x => x.Key.Split('#')[0]).Distinct())
                    {
                        if (!roleProblems.ContainsKey(role)) roleProblems[role] = problem;
                    }

                    Unresolve(check, problem);
                }

                if (exercised == 0)
                {
                    check.Status = "not-exercised";
                    check.Answer = "no design-day data set to check (none expected, or the coverage is unresolved: S10-C10)";
                }
                else
                {
                    Verify(check, "every design-day data set is calendar-shaped: 8760 elements at lower bound " + offset + ", populated exactly at its TBD year day, -1 elsewhere");
                }
            }

            /// <summary>
            /// The year day of a data set's single TBD design day, from the raw TBD read (tbd-snapshot.json), matched to the
            /// coverage group by role and exact name; the two reads must be of the same TBD and agree on the day.
            /// </summary>
            private int YearDay(string role, string label, DesignCoverageGroup group, out string problem)
            {
                problem = null;
                if (group.Days.Count != 1)
                {
                    problem = label + ": its TBD group holds " + group.Days.Count + " design day(s); only the single-design-day calendar representation was measured";
                    return 0;
                }

                if (tbd == null)
                {
                    problem = label + ": no TBD snapshot, so the design day's year day is unknown";
                    return 0;
                }

                if (coverage?.Sha256After == null || coverage.Sha256After != tbd.Reader?.TbdSha256After)
                {
                    problem = label + ": the coverage read (" + (coverage?.Sha256After ?? "no sha256") + ") and the TBD snapshot (" + (tbd.Reader?.TbdSha256After ?? "no sha256") + ") are not reads of the same TBD";
                    return 0;
                }

                List<NativeDesignDayGroup> groups = (role == Cooling ? tbd.CoolingDesignDays : tbd.HeatingDesignDays)?.Where(x => string.Equals(x.Name, group.Name, StringComparison.Ordinal)).ToList() ?? new List<NativeDesignDayGroup>();
                if (groups.Count != 1 || groups[0].Days.Count != 1)
                {
                    problem = label + ": the TBD snapshot holds " + groups.Count + " " + role + " group(s) named '" + group.Name + "'" + (groups.Count == 1 ? " with " + groups[0].Days.Count + " design day(s)" : string.Empty) + "; exactly one with one design day is required";
                    return 0;
                }

                NativeDesignDay native = groups[0].Days[0];
                DesignCoverageDay covered = group.Days[0];
                bool sameRepetitions = native.Repetitions?.HasNumber == true && covered.Repetitions?.HasNumber == true ? native.Repetitions.Value == covered.Repetitions.Value : native.Repetitions?.HasNumber != true && covered.Repetitions?.HasNumber != true;
                if (!string.Equals(native.DayType, covered.DayType, StringComparison.Ordinal) || !sameRepetitions)
                {
                    problem = label + ": the TBD snapshot and the coverage read disagree on the design day (day type '" + native.DayType + "' / '" + covered.DayType + "', repetitions " + native.Repetitions + " / " + covered.Repetitions + ")";
                    return 0;
                }

                if (native.YearDay?.HasNumber != true)
                {
                    problem = label + ": the TBD design day's yearDay is " + (native.YearDay?.ToString() ?? "not read") + ", so the expected design-day hours are unknown";
                    return 0;
                }

                double value = native.YearDay.Value;
                if (value != Math.Floor(value) || value < 1 || value > 365)
                {
                    problem = label + ": the TBD design day's yearDay " + Num(value) + " is not a day 1..365 of the simulated year";
                    return 0;
                }

                return (int)value;
            }

            /// <summary>
            /// Why a data set zone's series are not calendar-shaped at the design day starting at <paramref name="start"/>, or
            /// null. The role's load is required; every other series that was read is checked too (an unread one is used by no
            /// row: S10-C9 checks a gain series again before taking it as a time signal).
            /// </summary>
            private string CalendarSeries(TsdResultZone zone, string load, int start, string label, ref int arrays)
            {
                if (!zone.Arrays.ContainsKey(load)) return label + " zone '" + zone.Name + "' " + load + " was not read";
                foreach (KeyValuePair<string, ResultSeries> pair in zone.Arrays.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (pair.Key != load && pair.Value?.IsRead != true) continue;
                    string what = label + " zone '" + zone.Name + "' " + pair.Key;
                    string problem = Shape(pair.Value, what);
                    if (problem == null && (pair.Value.LowerBound != offset || pair.Value.Values.Length != AnnualHours))
                    {
                        problem = what + " has " + pair.Value.Values.Length + " elements at lower bound " + pair.Value.LowerBound + "; the calendar representation is " + AnnualHours + " at the N-5 lower bound " + offset + " (never compacted, padded or re-based)";
                    }

                    if (problem == null) problem = DayMask(pair.Value.Values, start, offset, pair.Key == "heatingLoad" || pair.Key == "coolingLoad", what);
                    if (problem != null) return problem;
                    arrays++;
                }

                return null;
            }

            /// <summary>
            /// S10-C9 (R1): the index and day-origin convention of the design-day series, per role, from independent evidence.
            /// Index: every series is at the N-5 lower bound, and Tas's own GetPeakZoneGains (whose value and index rows are
            /// located on the annual data, where N-5 fixes the index as the CLR index) holds each positive design-day maximum and
            /// the CLR index of one of its tied elements inside the TBD design day (S10-C7: an index at an hour outside the day,
            /// or at a non-maximum, contradicts it). The day block itself is the TBD year day's (S10-C7). Within-day origin: an
            /// hour-varying TBD gain schedule of the design days' day type is found in the TSD design-day gains of that day at
            /// offset 0 only (its first element = 00:00-01:00). A matching lower bound alone, or agreement with the importer, is
            /// never taken as evidence. Without index evidence the design-day hours and the design-governed legacy index are
            /// BLOCKED; without a time signal the design-day hours are, except where every hour of the day is a tied maximum
            /// (then every origin gives the same hour set: <see cref="ZoneRows"/>).
            /// </summary>
            private void CheckDesignConvention()
            {
                CalibrationCheck check = Check("S10-C9", "Is the design-day index and day-origin convention established from evidence (N-5 lower bound; the TBD year day's block of CLR indices, S10-C7; Tas's peak index = CLR index of an in-day maximum; hour 0 at the block's first index from the TBD gain schedule)?");
                MeasureLayout(check);
                bool exercised = false;
                foreach (string role in Roles)
                {
                    List<TsdDesignDataset> datasets = Datasets(role);
                    if (roleProblems.ContainsKey(role) || datasets.Count == 0) continue;

                    exercised = true;
                    string index = DesignIndex(role, datasets, check.Evidence);
                    string origin = index ?? DesignOrigin(role, datasets, check.Evidence);
                    if (index != null) indexProblems[role] = index;
                    if (origin != null)
                    {
                        originProblems[role] = origin;
                        Unresolve(check, origin);
                        if (index == null && FullDayTies(role, datasets))
                        {
                            check.Evidence.Add(role + ": the within-day origin is unresolved, but every positive design-day zone maximum of the role holds all 24 hours of its design day, so its hour-of-day rows are the same for every origin and are derived; only a role with a narrower tie set stays BLOCKED");
                        }
                    }
                }

                if (!exercised)
                {
                    check.Status = "not-exercised";
                    check.Answer = "no design-day data set (none expected, or coverage or shape unresolved: S10-C10, S10-C7)";
                }
                else
                {
                    Verify(check, "native index = CLR index at lower bound " + offset + ", hour of day = (index - 1) mod 24, for every role with design-day data sets");
                }
            }

            private string DesignIndex(string role, List<TsdDesignDataset> datasets, List<string> evidence)
            {
                string load = role == Cooling ? "coolingLoad" : "heatingLoad";
                foreach (TsdDesignDataset dataset in datasets)
                {
                    foreach (TsdResultZone zone in dataset.Zones)
                    {
                        int lowerBound = zone.Arrays[load].LowerBound;
                        if (lowerBound != offset)
                        {
                            return role + " design-day data set '" + dataset.Name + "' zone '" + zone.Name + "' " + load + " is at lower bound " + lowerBound + "; the convention requires the N-5 lower bound " + offset + " (index = CLR index, 1-based): an unexpected bound is not evidence of another convention, so the index and hour of day are unresolved";
                        }
                    }
                }

                if (layout == null)
                {
                    return role + " design-day index unresolved: " + layoutProblem;
                }

                int informative = 0;
                foreach (TsdDesignDataset dataset in datasets)
                {
                    NativeTable table = dataset.PeakZoneGains;
                    if (table == null || table.Kind != FieldKind.Finite)
                    {
                        return role + " design-day data set '" + dataset.Name + "': GetPeakZoneGains was not read (" + (table == null ? "absent" : table.Kind + " " + table.Detail) + "), so its native index cannot be checked";
                    }

                    foreach (TsdResultZone zone in dataset.Zones)
                    {
                        ResultSeries series = zone.Arrays[load];
                        (double max, List<int> ties) = DayPeak(series.Values, datasetStarts[role + "#" + dataset.Index]);
                        if (max <= 0) continue;

                        string mismatch = TableMismatch(table, zone.Index - 1, max, ties.Select(x => x + series.LowerBound));
                        if (mismatch != null)
                        {
                            return role + " design-day data set '" + dataset.Name + "' zone '" + zone.Name + "': " + mismatch + ", so the native index convention is contradicted";
                        }

                        informative++;
                    }
                }

                evidence.Add(role + ": " + informative + " design-day zone peak(s) found in GetPeakZoneGains at value row " + layout.Value.value + ", index row " + layout.Value.index + " with index = CLR index of an in-day maximum");
                return informative == 0 ? role + ": no design-day zone has a positive peak, so the native index is not checked" : null;
            }

            /// <summary>True when every positive design-day zone maximum of the role is tied at all 24 hours of its design day.</summary>
            private bool FullDayTies(string role, List<TsdDesignDataset> datasets)
            {
                string load = role == Cooling ? "coolingLoad" : "heatingLoad";
                bool any = false;
                foreach (TsdDesignDataset dataset in datasets)
                {
                    foreach (TsdResultZone zone in dataset.Zones)
                    {
                        (double max, List<int> ties) = DayPeak(zone.Arrays[load].Values, datasetStarts[role + "#" + dataset.Index]);
                        if (max <= 0) continue;
                        if (ties.Count != HoursPerDay) return false;
                        any = true;
                    }
                }

                return any;
            }

            /// <summary>Why the table column does not hold <paramref name="max"/> and one of <paramref name="indices"/>, or null.</summary>
            private string TableMismatch(NativeTable table, int column, double max, IEnumerable<int> indices)
            {
                if (column < 0 || layout.Value.value >= table.Cells.Count || layout.Value.index >= table.Cells.Count || column >= table.Cells[layout.Value.value].Count || column >= table.Cells[layout.Value.index].Count)
                {
                    return "column " + column + " is outside the GetPeakZoneGains table (" + string.Join("x", table.Lengths ?? new int[0]) + ")";
                }

                NativeField value = table.Cells[layout.Value.value][column], index = table.Cells[layout.Value.index][column];
                if (value?.HasNumber != true || value.Value != max)
                {
                    return "GetPeakZoneGains value " + value + " is not the series maximum " + Num(max);
                }

                if (index?.HasNumber != true || !indices.Any(x => x == index.Value))
                {
                    return "GetPeakZoneGains index " + index + " is not the CLR index of a tied maximum (" + string.Join(",", indices.Take(10)) + ")";
                }

                return null;
            }

            private (int value, int index)? layout;
            private string layoutProblem = "the annual GetPeakZoneGains layout was not measured";

            /// <summary>
            /// Which rows of the annual GetPeakZoneGains tables hold a zone's peak and its index, measured where N-5 fixes the
            /// index (CLR index = hour + 1): exactly one (value row, index row) pair must fit every positive annual zone peak.
            /// </summary>
            private void MeasureLayout(CalibrationCheck check)
            {
                List<(NativeTable table, string load)> tables = new List<(NativeTable, string)>();
                foreach (string load in new[] { "heatingLoad", "coolingLoad" })
                {
                    NativeTable table = null;
                    tsd?.AnnualPeakZoneGains?.TryGetValue(load, out table);
                    if (table == null || table.Kind != FieldKind.Finite)
                    {
                        layoutProblem = "the annual GetPeakZoneGains " + load + " table was not read, so its layout cannot be measured";
                        return;
                    }

                    tables.Add((table, load));
                }

                int rows = tables.Min(x => x.table.Cells.Count);
                List<(int value, int index)> fits = new List<(int, int)>();
                int peaks = 0;
                for (int value = 0; value < rows; value++)
                {
                    for (int index = 0; index < rows; index++)
                    {
                        if (index == value) continue;
                        bool fit = true;
                        int n = 0;
                        foreach ((NativeTable table, string load) in tables)
                        {
                            foreach (ZoneSeries zone in zones)
                            {
                                double[] values = zone.Get(load, out string problem);
                                if (problem != null) continue;
                                (double max, List<int> ties) = Peak(values);
                                if (max <= 0) continue;
                                int column = zone.Zone.Index - 1;
                                NativeField v = column >= 0 && column < table.Cells[value].Count ? table.Cells[value][column] : null;
                                NativeField i = column >= 0 && column < table.Cells[index].Count ? table.Cells[index][column] : null;
                                if (v?.HasNumber != true || v.Value != max || i?.HasNumber != true || !ties.Any(x => x + offset == i.Value))
                                {
                                    fit = false;
                                }

                                n++;
                            }
                        }

                        peaks = n;
                        if (fit && n > 0) fits.Add((value, index));
                    }
                }

                if (fits.Count == 1)
                {
                    layout = fits[0];
                    check.Evidence.Add("annual GetPeakZoneGains layout: value row " + layout.Value.value + ", index row " + layout.Value.index + " (rows from the table's lower bound) fit all " + peaks + " positive annual zone peak(s) with index = CLR index (N-5)");
                }
                else
                {
                    layoutProblem = fits.Count == 0
                        ? "no row pair of the annual GetPeakZoneGains tables holds every positive annual zone peak and the CLR index of a tied maximum (" + peaks + " peak(s))"
                        : "the annual GetPeakZoneGains layout is ambiguous: " + fits.Count + " row pairs fit";
                    check.Evidence.Add(layoutProblem);
                }
            }

            private string DesignOrigin(string role, List<TsdDesignDataset> datasets, List<string> evidence)
            {
                int hourly = rules.Offset(CalibrationRules.HourlySlot, 1, out bool hourlyVerified);
                if (!hourlyVerified)
                {
                    return role + ": the N-5 hourly-slot rule is not verified, so no TBD schedule can be placed in the day";
                }

                string load = role == Cooling ? "coolingLoad" : "heatingLoad";
                HashSet<int> offsets = null;
                int signals = 0;
                foreach (TsdDesignDataset dataset in datasets)
                {
                    DesignCoverageGroup group = datasetGroups[role + "#" + dataset.Index];
                    List<string> dayTypes = group.Days.Select(x => x.DayType).Distinct().ToList();
                    if (dayTypes.Count != 1 || dayTypes[0] == null)
                    {
                        return role + " design-day data set '" + dataset.Name + "': its TBD design days carry day type(s) [" + string.Join(", ", dayTypes) + "], so the schedules that apply are not established";
                    }

                    foreach (TsdResultZone zone in dataset.Zones)
                    {
                        List<NativeZone> tbdZones = tbd?.Zones?.Where(x => SameGuid(x.Guid, zone.Guid)).ToList() ?? new List<NativeZone>();
                        List<NativeInternalCondition> conditions = tbdZones.Count == 1 ? tbdZones[0].InternalConditions.Where(x => x.DayTypes.Contains(dayTypes[0])).ToList() : new List<NativeInternalCondition>();
                        if (conditions.Count != 1) continue;

                        ResultSeries loadSeries = zone.Arrays[load];
                        foreach ((string slot, string array) in new[] { ("ticLG", "lightingGain"), ("ticESG", "equipmentSensibleGain") })
                        {
                            double[] schedule = HourlySchedule(conditions[0], slot, hourly);
                            if (schedule == null || schedule.Max() <= 0 || schedule.All(x => x == schedule[0])) continue;

                            zone.Arrays.TryGetValue(array, out ResultSeries gains);
                            if (Shape(gains, array) != null || gains.LowerBound != loadSeries.LowerBound || gains.Values.Length != loadSeries.Values.Length) continue;

                            // Only the TBD year day's 24 elements (S10-C7); its first element is hour 0 at offset 0.
                            List<int> fit = Offsets(Day(gains.Values, datasetStarts[role + "#" + dataset.Index]), schedule);
                            signals++;
                            offsets = offsets == null ? new HashSet<int>(fit) : new HashSet<int>(offsets.Intersect(fit));
                            evidence.Add(role + " '" + dataset.Name + "' zone '" + zone.Name + "' " + array + " vs TBD " + slot + " of '" + conditions[0].Name + "' (" + dayTypes[0] + "): fits at offset(s) [" + string.Join(",", fit) + "]");
                        }
                    }
                }

                if (signals == 0)
                {
                    return role + ": no independent time signal (no hour-varying TBD gain schedule of the design days' day type with a TSD design-day gain array), so the hour of day of a design-day element is not established";
                }

                if (offsets.Count != 1 || !offsets.Contains(0))
                {
                    return role + ": the TBD gain schedules fit the TSD design-day gains at offset(s) [" + string.Join(",", offsets.OrderBy(x => x)) + "] over " + signals + " signal(s); the convention requires offset 0 only (index 1 = 00:00-01:00)";
                }

                return null;
            }

            /// <summary>The 24 hourly values x factor of an hourly-typed (TypeCode 2) gain profile, or null.</summary>
            private static double[] HourlySchedule(NativeInternalCondition condition, string slot, int hourly)
            {
                NativeProfile profile = condition.GainProfiles != null && condition.GainProfiles.TryGetValue(slot, out NativeProfile p) ? p : null;
                if (profile == null || profile.Kind != FieldKind.Finite || profile.TypeCode != 2 || profile.Factor?.HasNumber != true || profile.Hourly == null) return null;
                double[] result = new double[HoursPerDay];
                for (int h = 0; h < HoursPerDay; h++)
                {
                    if (!profile.Hourly.TryGetValue(h + hourly, out NativeField field) || field?.HasNumber != true) return null;
                    result[h] = field.Value * profile.Factor.Value;
                }

                return result;
            }

            // -- rows ------------------------------------------------------------------------------------------------

            private void ModelRows()
            {
                string balance = zones.Select(x => x.BalanceProblem).FirstOrDefault(x => x != null) ?? (zones.Count == 0 ? "no zones" : null);
                foreach (string type in new[] { Heating, Cooling })
                {
                    string profile = type == Heating ? "heatingProfile" : "coolingProfile";
                    buildingSeries.TryGetValue(profile, out double[] values);
                    buildingProblems.TryGetValue(profile, out string problem);
                    problem = problem ?? (balance == null ? null : "the zone load units are unresolved: " + balance);

                    ReferenceRow consumption = Row("SAM", "model", null, type, "consumption", "R2", "kWh");
                    ReferenceRow peak = Row("SAM", "model", null, type, "peakLoad", "R1", "kW");
                    ReferenceRow hour = Row("SAM", "model", null, type, "peakHour", "Hour", "hourOfYear");
                    ReferenceRow b1aConsumption = Row("B1a", "model", null, type, "consumption", "R2", "kWh");
                    ReferenceRow b1aPeak = Row("B1a", "model", null, type, "peakLoad", "R1", "kW");
                    ReferenceRow b1aHour = Row("B1a", "model", null, type, "peakHour", "Hour", "hourOfYear");
                    if (problem != null)
                    {
                        foreach (ReferenceRow row in new[] { consumption, peak, hour, b1aConsumption, b1aPeak, b1aHour }) Block(row, problem);
                        continue;
                    }

                    double sum = 0;
                    foreach (double value in values) sum += value;
                    (double max, List<int> ties) = Peak(values);
                    Expect(consumption, sum / 1000, "sum of the building " + profile + " (W x 1 h) / 1000");
                    Expect(b1aConsumption, sum / 1000, consumption.Reason);
                    Expect(peak, max / 1000, "max of the building " + profile + " / 1000");
                    Expect(b1aPeak, max / 1000, peak.Reason);
                    if (max == 0)
                    {
                        // SAM stores IndexOf(0) = hour 0 for an all-zero profile: documented and accepted (S10 OD-2); B1a has no hour.
                        Expect(hour, 0, "all-zero profile: SAM's documented placeholder hour 0 (no peak hour)");
                        NoValue(b1aHour, "all-zero profile: a zero peak has no hour (zero rule)");
                    }
                    else
                    {
                        Expect(hour, ties[0], "earliest maximum of the building " + profile + " (model tie rule; " + ties.Count + " tied)");
                        Expect(b1aHour, ties[0], hour.Reason);
                    }

                    model.Observations.Add("model " + type + ": max " + Num(max) + " W at " + ties.Count + " tied hour(s), first " + (ties.Count > 0 ? ties[0].ToString(ci) : "-"));
                }

                string areaProblem = identityProblem ?? zones.Select(x => x.Zone.FloorArea?.HasNumber == true && x.Zone.Volume?.HasNumber == true ? null : "zone '" + x.Zone.Name + "' floor area or volume not read (" + x.Zone.FloorArea + ", " + x.Zone.Volume + ")").FirstOrDefault(x => x != null) ?? (zones.Count == 0 ? "no zones" : null);
                foreach (string quantity in new[] { "floorArea", "volume" })
                {
                    string unit = quantity == "floorArea" ? "m2" : "m3";
                    ReferenceRow sam = Row("SAM", "model", null, null, quantity, "R1", unit);
                    ReferenceRow b1a = Row("B1a", "model", null, null, quantity, "R1", unit);
                    if (areaProblem != null)
                    {
                        Block(sam, areaProblem);
                        Block(b1a, areaProblem);
                        continue;
                    }

                    double sum = 0;
                    foreach (ZoneSeries zone in zones) sum += quantity == "floorArea" ? zone.Zone.FloorArea.Value : zone.Zone.Volume.Value;
                    Expect(sam, sum, "sum of the TSD zone " + quantity + " values");
                    Expect(b1a, sum, sam.Reason);
                }

                Text(Row("SAM", "model", null, null, "source", "Text", null), TasSource, "the model result's Source is the Tas converter's assembly");
                Text(Row("SAM", "model", null, null, "unitsState", "Text", null), "Marked", "ResultUnitsVersion 1: consumption kWh, peaks kW");
            }

            private void IdentityRows()
            {
                int count = tsd?.Zones?.Count ?? 0;
                Count(Row("Identity", "model", null, null, "samSpaceCount", "Count", null), count, "one SAM space per TSD zone");
                Count(Row("Identity", "model", null, null, "b1aSpaceCount", "Count", null), count, "one B1a space per TSD zone");
                Count(Row("Identity", "model", null, null, "unrelatedSpaceResults", "Count", null), 0, "every space result is related to a space");
                Text(Row("Identity", "model", null, null, "b1aResultSources", "Text", null), TasSource, "the B1a result sources are exactly the Tas source");
                Text(Row("Identity", "model", null, null, "b1aState", "Text", null), "Success", "the B1a run state");
                foreach (ZoneSeries zone in zones)
                {
                    ReferenceRow space = Row("Identity", zone.Zone.Guid, zone.Zone.Name, null, "samSpace", "Count", null);
                    ReferenceRow b1a = Row("Identity", zone.Zone.Guid, zone.Zone.Name, null, "b1aSpace", "Count", null);
                    if (identityProblem != null)
                    {
                        Block(space, identityProblem);
                        Block(b1a, identityProblem);
                        continue;
                    }

                    Count(space, 1, "exactly one SAM space with the zone's exact name");
                    Count(b1a, 1, "exactly one B1a space with that SAM space's GUID and name");
                }
            }

            private void ZoneRows(ZoneSeries zone, string type)
            {
                string guid = zone.Zone.Guid, name = zone.Zone.Name;
                string loadName = type == Heating ? "heatingLoad" : "coolingLoad";
                ReferenceRow R(string layer, string quantity, string @class, string unit) => Row(layer, guid, name, type, quantity, @class, unit);

                ReferenceRow count = R("SAM", "tasResultCount", "Count", null);
                ReferenceRow reference = R("SAM", "reference", "Text", null);
                ReferenceRow annualLoad = R("SAM", "annualPeak.load", "R0", "W");
                ReferenceRow annualHour = R("SAM", "annualPeak.hourOfYear", "Hour", "hourOfYear");
                ReferenceRow annualHourOfDay = R("SAM", "annualPeak.hourOfDay", "Hour", "hourOfDay");
                ReferenceRow ddLoad = R("SAM", "designDayPeak.load", "R0", "W");
                ReferenceRow ddHourOfDay = R("SAM", "designDayPeak.hourOfDay", "Hour", "hourOfDay");
                ReferenceRow ddName = R("SAM", "designDayPeak.name", "Text", null);
                ReferenceRow sizing = R("SAM", "legacy.sizingMethod", "Text", null);
                ReferenceRow legacyLoad = R("SAM", "legacy.load", "R0", "W");
                ReferenceRow legacyIndex = R("SAM", "legacy.loadIndex", "Hour", "tasIndex");
                ReferenceRow designLoad = R("SAM", "designLoad", "R0", "W");
                ReferenceRow unmet = R("SAM", "unmetHours", "R0", "h");
                ReferenceRow b1aPeak = R("B1a", "peakLoad", "R0", "W");
                ReferenceRow b1aHour = R("B1a", "peakHour", "Hour", "hourOfYear");
                ReferenceRow b1aDesign = R("B1a", "designLoad", "R0", "W");
                ReferenceRow b1aUnmet = R("B1a", "unmetHours", "R0", "h");

                Count(count, 1, "exactly one result of this load type, Source " + TasSource + ", related to the zone's SAM space");
                Text(reference, guid, "the result's Reference is the TSD zone GUID");

                // Annual peak (S10 OD-2): max of the annual zone load; zero rule; any tied maximum accepted.
                double[] annual = zone.Get(loadName, out string annualProblem);
                annualProblem = annualProblem ?? zone.BalanceProblem;
                (double max, List<int> ties) = annual == null ? (double.NaN, new List<int>()) : Peak(annual);
                if (annualProblem != null)
                {
                    foreach (ReferenceRow row in new[] { annualLoad, annualHour, annualHourOfDay, b1aPeak, b1aHour }) Block(row, annualProblem);
                }
                else
                {
                    string derivation = "max of the annual zone " + loadName + " (8760 h)";
                    Expect(annualLoad, max, derivation);
                    Expect(b1aPeak, max, derivation);
                    if (max == 0)
                    {
                        NoValue(annualHour, "zero peak: no hour");
                        NoValue(annualHourOfDay, "zero peak: no hour");
                        NoValue(b1aHour, "zero peak: no hour (zero rule)");
                    }
                    else
                    {
                        string tie = ties.Count + " tied maximum/maxima" + (ties.Count > 1 ? ", any accepted (S10 tie rule)" : string.Empty);
                        Hours(annualHour, ties.Select(x => (double)x), "0-based hour = position (CLR index - " + offset + "); " + tie);
                        Hours(annualHourOfDay, ties.Select(x => (double)(x % HoursPerDay)).Distinct(), "hour of day of the tied maxima");
                        Hours(b1aHour, ties.Select(x => (double)x), annualHour.Reason);
                    }

                    model.Observations.Add("zone '" + name + "' " + type + " annual: max " + Num(max) + " W, " + ties.Count + " tied hour(s)" + (ties.Count > 0 ? ", earliest " + ties[0] + (ties.Count > 1 ? ", all " + string.Join(",", ties.Take(20)) : string.Empty) : string.Empty));
                    NativeCrossCheck(tsd.AnnualPeakZoneGains, loadName, zone.Zone.Index, max, ties, "annual " + type, name);
                }

                // Design-day peak: max over the role's data sets, the first data set winning a tie (documented importer rule).
                DesignPeak dd = DesignDay(zone, type);
                if (dd.Problem == null && dd.Present && zone.BalanceProblem != null)
                {
                    // The design-day arrays are the same Tas load arrays: their unit and role rest on the zone's balance check.
                    dd.Problem = zone.BalanceProblem;
                }

                if (dd.Problem != null)
                {
                    foreach (ReferenceRow row in new[] { ddLoad, ddHourOfDay, ddName }) Block(row, dd.Problem);
                }
                else if (!dd.Present)
                {
                    NoValue(ddLoad, "the completely read TBD holds no " + type + " design-day group and the TSD no data set (S10-C10): no design-day peak is expected");
                    NoValue(ddHourOfDay, ddLoad.Reason);
                    NoValue(ddName, ddLoad.Reason);
                }
                else
                {
                    Expect(ddLoad, dd.Max, "max over the " + type + " design-day data sets of the zone's " + loadName + " (" + dd.DatasetsTied + " data set(s) tied; the first wins)");
                    Text(ddName, dd.Name, "the winning data set's name");
                    if (dd.Max == 0)
                    {
                        NoValue(ddHourOfDay, "zero peak: no hour");
                    }
                    else if (indexProblems.TryGetValue(type, out string ddIndexProblem))
                    {
                        // The load does not depend on the convention; the hour does (S10-C9).
                        Block(ddHourOfDay, "design-day hour of day unresolved (S10-C9): " + ddIndexProblem);
                    }
                    else if (originProblems.TryGetValue(type, out string ddOriginProblem))
                    {
                        if (dd.Ties.Count == HoursPerDay)
                        {
                            // Every hour of the TBD design day holds the maximum: a within-day rotation maps the tie set onto
                            // itself, so the hour set does not depend on the unresolved origin.
                            Hours(ddHourOfDay, Enumerable.Range(0, HoursPerDay).Select(x => (double)x), "all 24 hours of the TBD design day are tied maxima, so every hour of day is accepted whatever the within-day origin (S10-C9 origin unresolved: " + ddOriginProblem + ")");
                        }
                        else
                        {
                            Block(ddHourOfDay, "design-day hour of day unresolved (S10-C9): " + ddOriginProblem);
                        }
                    }
                    else
                    {
                        Hours(ddHourOfDay, dd.Ties.Select(x => (double)((x + dd.LowerBound - 1) % HoursPerDay)).Distinct(), "hour of day = (index - 1) mod 24, index = CLR index (S10-C9); " + dd.Ties.Count + " tied");
                    }

                    model.Observations.Add("zone '" + name + "' " + type + " design day: max " + Num(dd.Max) + " W in '" + dd.Name + "', " + dd.Ties.Count + " tied position(s)");
                }

                // Legacy governing projection (documented, unchanged): the design day unless the annual peak is strictly larger.
                string governingProblem = annualProblem ?? dd.Problem;
                if (governingProblem != null)
                {
                    foreach (ReferenceRow row in new[] { sizing, legacyLoad, legacyIndex }) Block(row, governingProblem);
                }
                else
                {
                    bool designGoverns = dd.Present && !(max > dd.Max);
                    double governing = designGoverns ? dd.Max : max;
                    List<int> governingTies = designGoverns ? dd.Ties : ties;
                    Text(sizing, designGoverns ? (type == Cooling ? "Cooling Design Day" : "Heating Design Day") : "Simulation", designGoverns ? "the design day governs (annual peak not strictly larger)" : "the annual peak governs");
                    if (governing == 0)
                    {
                        Expect(legacyLoad, LegacyZeroSentinel, "zero governing peak: Tas's -1 at index 0 (documented legacy sentinel)");
                        Expect(legacyIndex, 0, "zero governing peak: index 0");
                    }
                    else
                    {
                        Expect(legacyLoad, governing, "the governing peak load");
                        if (!designGoverns)
                        {
                            Hours(legacyIndex, governingTies.Select(x => (double)(x + offset)), "Tas's raw 1-based annual index = CLR index (N-5); " + governingTies.Count + " tied");
                        }
                        else if (indexProblems.TryGetValue(type, out string legacyIndexProblem))
                        {
                            Block(legacyIndex, "design-day index unresolved (S10-C9): " + legacyIndexProblem);
                        }
                        else
                        {
                            Hours(legacyIndex, governingTies.Select(x => (double)(x + dd.LowerBound)), "Tas's raw design-day index = CLR index (S10-C9); " + governingTies.Count + " tied");
                        }
                    }

                    model.Observations.Add("zone '" + name + "' " + type + " governing basis: " + (designGoverns ? "design day" : "annual"));
                }

                // Design load: TBD sizing, read independently from the TBD.
                NativeZone tbdZone = TbdZone(guid, out string tbdZoneProblem);
                NativeField maxLoad = tbdZone == null ? null : type == Heating ? tbdZone.MaxHeatingLoad : tbdZone.MaxCoolingLoad;
                string designProblem = tbdZoneProblem ?? (maxLoad?.HasNumber == true ? null : "TBD zone '" + tbdZone.Name + "' max" + type + "Load is " + (maxLoad?.ToString() ?? "not read"));
                if (designProblem != null)
                {
                    Block(designLoad, designProblem);
                    Block(b1aDesign, designProblem);
                }
                else
                {
                    Expect(designLoad, maxLoad.Value, "TBD zone max" + type + "Load (sizing)");
                    Expect(b1aDesign, maxLoad.Value, designLoad.Reason);
                }

                // Unmet hours: re-derived from the TSD air temperature and the TBD thermostat (a check of SAM's stated definition).
                string unmetProblem = null;
                double[] dryBulb = zone.Get("dryBulbTemp", out string dryBulbProblem);
                double[] setpoint = tbdZone != null ? Setpoint(tbdZone, type == Heating ? "ticLL" : "ticUL", out unmetProblem) : null;
                unmetProblem = dryBulbProblem ?? tbdZoneProblem ?? unmetProblem;
                if (unmetProblem != null)
                {
                    Block(unmet, unmetProblem);
                    Block(b1aUnmet, unmetProblem);
                }
                else
                {
                    int value = UnmetHours(dryBulb, setpoint, type == Heating);
                    string derivation = type == Heating ? "hours with TSD dryBulbTemp + 0.5 < TBD ticLL" : "hours with TSD dryBulbTemp - 0.5 > TBD ticUL";
                    Expect(unmet, value, derivation);
                    Expect(b1aUnmet, value, derivation);
                }
            }

            private DesignPeak DesignDay(ZoneSeries zone, string type)
            {
                DesignPeak result = new DesignPeak();
                if (roleProblems.TryGetValue(type, out string problem))
                {
                    // Coverage (S10-C10) or shape (S10-C7) unresolved: neither a peak nor its absence can be established.
                    result.Present = true;
                    result.Problem = problem;
                    return result;
                }

                // With the coverage verified, no data set of the role means the TBD holds no group of it: an expected absence.
                List<TsdDesignDataset> datasets = Datasets(type);
                if (datasets.Count == 0) return result;

                result.Present = true;
                string loadName = type == Heating ? "heatingLoad" : "coolingLoad";
                double best = double.NaN;
                foreach (TsdDesignDataset dataset in datasets)
                {
                    List<TsdResultZone> matches = dataset.Zones.Where(x => SameGuid(x.Guid, zone.Zone.Guid)).ToList();
                    if (matches.Count != 1)
                    {
                        result.Problem = type + " design-day data set '" + dataset.Name + "' holds " + matches.Count + " zone(s) with GUID " + zone.Zone.Guid;
                        return result;
                    }

                    // Over the TBD year day only (S10-C7), keeping the series' own positions, so the CLR index stays the raw one.
                    (double max, List<int> ties) = DayPeak(matches[0].Arrays[loadName].Values, datasetStarts[type + "#" + dataset.Index]);
                    if (double.IsNaN(best) || max > best)
                    {
                        best = max;
                        result.Max = max;
                        result.Ties = ties;
                        result.LowerBound = matches[0].Arrays[loadName].LowerBound;
                        result.Name = dataset.Name;
                        result.DatasetsTied = 1;
                    }
                    else if (max == best)
                    {
                        result.DatasetsTied++;
                    }

                    NativeCrossCheck(new Dictionary<string, NativeTable> { [loadName] = dataset.PeakZoneGains }, loadName, matches[0].Index, max, ties, type + " design day '" + dataset.Name + "'", zone.Zone.Name);
                }

                return result;
            }

            /// <summary>The 8760 hourly setpoints of a zone's thermostat slot: calendar day type (N-6) -> the zone's internal condition carrying it -> its profile (N-5) x factor.</summary>
            private double[] Setpoint(NativeZone zone, string slot, out string problem)
            {
                problem = null;
                int first = rules.Offset(CalibrationRules.Calendar, 1, out bool calendarVerified);
                int hourly = rules.Offset(CalibrationRules.HourlySlot, 1, out bool hourlyVerified);
                int yearly = rules.Offset(CalibrationRules.YearlyIndex, 1, out bool yearlyVerified);
                if (!calendarVerified || !hourlyVerified)
                {
                    problem = "the N-6 calendar or N-5 hourly-slot rule is not verified";
                    return null;
                }

                if (tbd?.Calendar?.Days == null)
                {
                    problem = "the TBD snapshot holds no calendar";
                    return null;
                }

                double[] result = new double[AnnualHours];
                for (int d = 0; d < 365; d++)
                {
                    List<NativeCalendarDay> days = tbd.Calendar.Days.Where(x => x != null && x.Index == d + first).ToList();
                    if (days.Count != 1 || days[0].DayType == null)
                    {
                        problem = "TBD calendar day " + (d + first) + " was read " + days.Count + " time(s) or has no day type";
                        return null;
                    }

                    List<NativeInternalCondition> conditions = zone.InternalConditions.Where(x => x.DayTypes.Contains(days[0].DayType)).ToList();
                    if (conditions.Count != 1)
                    {
                        problem = "TBD zone '" + zone.Name + "' has " + conditions.Count + " internal condition(s) for day type '" + days[0].DayType + "'";
                        return null;
                    }

                    NativeProfile profile = conditions[0].ThermostatProfiles != null && conditions[0].ThermostatProfiles.TryGetValue(slot, out NativeProfile p) ? p : null;
                    if (profile == null || profile.Kind != FieldKind.Finite || profile.Factor?.HasNumber != true)
                    {
                        problem = "TBD zone '" + zone.Name + "' " + slot + " profile of '" + conditions[0].Name + "' is " + (profile == null ? "absent" : profile.Kind + " (" + profile.Detail + "), factor " + profile.Factor);
                        return null;
                    }

                    for (int h = 0; h < HoursPerDay; h++)
                    {
                        NativeField field;
                        switch (profile.TypeCode)
                        {
                            case 1:
                                field = profile.Value;
                                break;
                            case 2:
                                field = profile.Hourly != null && profile.Hourly.TryGetValue(h + hourly, out NativeField f) ? f : null;
                                break;
                            case 3:
                                field = yearlyVerified && profile.Yearly != null && profile.Yearly.IsRead && profile.Yearly.LowerBound == yearly && profile.Yearly.Values.Length == AnnualHours ? profile.Yearly.At(d * HoursPerDay + h + yearly) : null;
                                break;
                            default:
                                field = null;
                                break;
                        }

                        if (field?.HasNumber != true)
                        {
                            problem = "TBD zone '" + zone.Name + "' " + slot + " (type " + profile.Type + ") gives no number for day " + (d + 1) + " hour " + h + " (" + (field?.ToString() ?? "not read") + ")";
                            return null;
                        }

                        result[d * HoursPerDay + h] = field.Value * profile.Factor.Value;
                    }
                }

                return result;
            }

            private NativeZone TbdZone(string guid, out string problem)
            {
                problem = null;
                List<NativeZone> matches = tbd?.Zones?.Where(x => SameGuid(x.Guid, guid)).ToList() ?? new List<NativeZone>();
                if (matches.Count == 1) return matches[0];
                problem = matches.Count + " TBD zone(s) with GUID " + guid;
                return null;
            }

            private void NativeCrossCheck(Dictionary<string, NativeTable> tables, string loadName, int zoneIndex, double max, List<int> ties, string what, string zoneName)
            {
                if (tables == null || !tables.TryGetValue(loadName, out NativeTable table) || table == null || table.Kind != FieldKind.Finite)
                {
                    model.Observations.Add("GetPeakZoneGains " + what + " zone '" + zoneName + "': table not read");
                    return;
                }

                // Report the zone's column as read; the reference never uses it.
                int column = zoneIndex - 1;
                List<string> cells = table.Cells.Select(row => column >= 0 && column < row.Count ? row[column].ToString() : "-").ToList();
                model.Observations.Add("GetPeakZoneGains " + what + " zone '" + zoneName + "' (bounds " + string.Join(",", table.LowerBounds) + ", column " + column + "): " + string.Join(" | ", cells) + "; reference max " + Num(max) + ", positions + 1 " + string.Join(",", ties.Take(10).Select(x => x + 1)));
            }

            // -- row helpers -----------------------------------------------------------------------------------------

            private ReferenceRow Row(string layer, string scope, string zoneName, string loadType, string quantity, string @class, string unit)
            {
                ReferenceRow row = new ReferenceRow
                {
                    Id = layer + "|" + scope + "|" + (loadType ?? "-") + "|" + quantity,
                    Layer = layer,
                    Scope = scope,
                    ZoneName = zoneName,
                    LoadType = loadType,
                    Quantity = quantity,
                    Class = @class,
                    Unit = unit,
                    State = ExpectedState.Blocked,
                    Reason = "not derived",
                };
                model.Rows.Add(row);
                return row;
            }

            private CalibrationCheck Check(string id, string question)
            {
                CalibrationCheck check = new CalibrationCheck { Id = id, Question = question };
                model.Checks.Add(check);
                return check;
            }

            private static void Verify(CalibrationCheck check, string answer)
            {
                if (check.Status == "unresolved") return;
                check.Status = "verified";
                check.Answer = answer;
            }

            private static void Unresolve(CalibrationCheck check, string answer)
            {
                check.Status = "unresolved";
                check.Answer = check.Answer == null || check.Answer.Length == 0 ? answer : check.Answer + "; " + answer;
            }
        }

        private static void Expect(ReferenceRow row, double value, string reason)
        {
            row.State = ExpectedState.Value;
            row.Value = value;
            row.Allowed = null;
            row.Reason = reason;
        }

        private static void Count(ReferenceRow row, int value, string reason)
        {
            Expect(row, value, reason);
        }

        private static void Hours(ReferenceRow row, IEnumerable<double> allowed, string reason)
        {
            List<double> values = allowed.ToList();
            row.State = ExpectedState.Value;
            row.Value = values.Count > 0 ? values[0] : double.NaN;
            row.Allowed = values;
            row.Reason = reason;
        }

        private static void Text(ReferenceRow row, string text, string reason)
        {
            row.State = ExpectedState.Value;
            row.Text = text;
            row.Reason = reason;
        }

        private static void NoValue(ReferenceRow row, string reason)
        {
            row.State = ExpectedState.NoValue;
            row.Reason = reason;
        }

        private static void Block(ReferenceRow row, string reason)
        {
            row.State = ExpectedState.Blocked;
            row.Allowed = null;
            row.Reason = reason;
        }
    }
}
