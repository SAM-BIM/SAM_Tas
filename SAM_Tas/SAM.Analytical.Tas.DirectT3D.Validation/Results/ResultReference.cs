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

        public static ResultReferenceModel Build(TsdResultSnapshot tsd, NativeSnapshot tbd, CalibrationRules rules)
        {
            ResultReferenceModel result = new ResultReferenceModel
            {
                TsdPath = tsd?.Path,
                TsdSha256 = tsd?.Sha256After,
                TbdPath = tbd?.Reader?.TbdPath,
                TbdSha256 = tbd?.Reader?.TbdSha256After,
                RulesSha256 = rules?.SourceSha256,
            };

            Builder builder = new Builder(result, tsd, tbd, rules ?? CalibrationRules.None());
            builder.Run();
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
            public string Name;
            public int DatasetsTied;
        }

        private sealed class Builder
        {
            private readonly ResultReferenceModel model;
            private readonly TsdResultSnapshot tsd;
            private readonly NativeSnapshot tbd;
            private readonly CalibrationRules rules;
            private readonly List<ZoneSeries> zones = new List<ZoneSeries>();
            private string global;
            private int offset = 1;
            private string identityProblem;
            private readonly Dictionary<string, string> datasetProblems = new Dictionary<string, string>(StringComparer.Ordinal);

            public Builder(ResultReferenceModel model, TsdResultSnapshot tsd, NativeSnapshot tbd, CalibrationRules rules)
            {
                this.model = model;
                this.tsd = tsd;
                this.tbd = tbd;
                this.rules = rules;
            }

            public void Run()
            {
                CheckRun();
                CheckIdentity();
                ReadZones();
                CheckBalance();
                CheckDesignDatasets();

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

            private void CheckDesignDatasets()
            {
                CalibrationCheck check = Check("S10-C7", "Are the design-day series complete, day-aligned and of the expected length (24 x the TBD design days, or 24 x their repetitions)?");
                List<TsdDesignDataset> datasets = tsd?.DesignDatasets ?? new List<TsdDesignDataset>();
                if (datasets.Count == 0)
                {
                    check.Status = "not-exercised";
                    check.Answer = "the TSD has no design-day data sets";
                    return;
                }

                List<string> problems = new List<string>();
                foreach (TsdDesignDataset dataset in datasets)
                {
                    string label = dataset.Role + " design-day data set " + dataset.Index + " '" + dataset.Name + "'";
                    List<NativeDesignDayGroup> groups = dataset.Role == Cooling ? tbd?.CoolingDesignDays : tbd?.HeatingDesignDays;
                    List<NativeDesignDayGroup> named = groups?.Where(x => x.Name != null && string.Equals(x.Name, dataset.Name, StringComparison.Ordinal)).ToList() ?? new List<NativeDesignDayGroup>();
                    string problem = null;
                    List<int> expected = new List<int>();
                    if (named.Count != 1)
                    {
                        problem = label + ": " + named.Count + " TBD design-day group(s) of that role and name, so its expected length is unknown";
                    }
                    else
                    {
                        NativeDesignDayGroup group = named[0];
                        expected.Add(HoursPerDay * group.Days.Count);
                        if (group.Days.Count > 0 && group.Days.All(x => x.Repetitions?.HasNumber == true && x.Repetitions.Value >= 1 && x.Repetitions.Value == Math.Floor(x.Repetitions.Value)))
                        {
                            expected.Add(HoursPerDay * (int)group.Days.Sum(x => x.Repetitions.Value));
                        }

                        expected = expected.Where(x => x > 0).Distinct().ToList();
                        if (expected.Count == 0) problem = label + ": the TBD group has no design days";
                    }

                    if (problem == null && dataset.Zones.Count != (tsd?.Zones?.Count ?? 0))
                    {
                        problem = label + " has " + dataset.Zones.Count + " zone(s); the building data has " + (tsd?.Zones?.Count ?? 0);
                    }

                    string load = dataset.Role == Cooling ? "coolingLoad" : "heatingLoad";
                    HashSet<string> shapes = new HashSet<string>(StringComparer.Ordinal);
                    foreach (TsdResultZone zone in problem == null ? dataset.Zones : new List<TsdResultZone>())
                    {
                        zone.Arrays.TryGetValue(load, out ResultSeries series);
                        string shape = Shape(series, label + " zone '" + zone.Name + "' " + load);
                        if (shape == null && !expected.Contains(series.Values.Length))
                        {
                            shape = label + " zone '" + zone.Name + "' " + load + " has " + series.Values.Length + " elements; expected " + string.Join(" or ", expected) + " (24 per TBD design day or repetition)";
                        }

                        if (shape == null) shape = Sign(series.Values, label + " zone '" + zone.Name + "' " + load);
                        if (shape != null)
                        {
                            problem = shape;
                            break;
                        }

                        shapes.Add(series.LowerBound + ":" + series.Values.Length);
                    }

                    if (problem == null && shapes.Count > 1) problem = label + ": its zones' series differ in lower bound or length (" + string.Join(", ", shapes) + ")";
                    check.Evidence.Add(label + ": " + (problem ?? "series " + string.Join(", ", shapes) + " (lower bound:length), expected length " + string.Join(" or ", expected)));
                    if (problem != null)
                    {
                        datasetProblems[dataset.Role + "#" + dataset.Index] = problem;
                        problems.Add(problem);
                    }
                }

                if (problems.Count == 0) Verify(check, "every design-day series is complete and of an expected length"); else Unresolve(check, string.Join("; ", problems));
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
                    NoValue(ddLoad, "the TSD has no " + type + " design-day data set: no design-day peak");
                    NoValue(ddHourOfDay, ddLoad.Reason);
                    NoValue(ddName, ddLoad.Reason);
                }
                else
                {
                    Expect(ddLoad, dd.Max, "max over the " + type + " design-day data sets of the zone's " + loadName + " (" + dd.DatasetsTied + " data set(s) tied; the first wins)");
                    Text(ddName, dd.Name, "the winning data set's name");
                    if (dd.Max == 0) NoValue(ddHourOfDay, "zero peak: no hour");
                    else Hours(ddHourOfDay, dd.Ties.Select(x => (double)(x % HoursPerDay)).Distinct(), "hour of day = position mod 24 (series day-aligned); " + dd.Ties.Count + " tied");
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
                        Hours(legacyIndex, governingTies.Select(x => (double)(x + 1)), "Tas's raw 1-based index = position + 1; " + governingTies.Count + " tied");
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
                List<TsdDesignDataset> datasets = (tsd?.DesignDatasets ?? new List<TsdDesignDataset>()).Where(x => x.Role == type).OrderBy(x => x.Index).ToList();
                if (datasets.Count == 0) return result;

                result.Present = true;
                string loadName = type == Heating ? "heatingLoad" : "coolingLoad";
                double best = double.NaN;
                foreach (TsdDesignDataset dataset in datasets)
                {
                    if (datasetProblems.TryGetValue(dataset.Role + "#" + dataset.Index, out string problem))
                    {
                        result.Problem = problem;
                        return result;
                    }

                    List<TsdResultZone> matches = dataset.Zones.Where(x => SameGuid(x.Guid, zone.Zone.Guid)).ToList();
                    if (matches.Count != 1)
                    {
                        result.Problem = type + " design-day data set '" + dataset.Name + "' holds " + matches.Count + " zone(s) with GUID " + zone.Zone.Guid;
                        return result;
                    }

                    (double max, List<int> ties) = Peak(matches[0].Arrays[loadName].Values);
                    if (double.IsNaN(best) || max > best)
                    {
                        best = max;
                        result.Max = max;
                        result.Ties = ties;
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
