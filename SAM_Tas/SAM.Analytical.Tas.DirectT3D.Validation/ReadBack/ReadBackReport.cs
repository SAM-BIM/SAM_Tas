// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.InputEquivalence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.ReadBack
{
    /// <summary>
    /// The read-back exit codes. 0 means PASS and nothing else; every other S07 status has its own code, and an
    /// infrastructure failure (licence, COM, open, a changed frozen input, a reader that was not a fresh process) is
    /// distinct from an engineering FAIL.
    /// </summary>
    public static class ReadBackExit
    {
        public const int Pass = 0;
        public const int Fail = 10;
        public const int BlockedTechnical = 11;
        public const int BlockedOwner = 12;
        public const int NotRun = 13;
        public const int AcceptedLimitation = 14;
        public const int Usage = 64;
        public const int Infrastructure = 70;

        public static int Of(InputEquivalence.GateStatus status)
        {
            switch (status)
            {
                case GateStatus.Pass: return Pass;
                case GateStatus.Fail: return Fail;
                case GateStatus.BlockedTechnical: return BlockedTechnical;
                case GateStatus.BlockedOwnerDecision: return BlockedOwner;
                case GateStatus.NotRun: return NotRun;
                default: return AcceptedLimitation;
            }
        }
    }

    public sealed class ReportComparison
    {
        public double Difference { get; set; }
        public double RelativeDifference { get; set; }
        public bool AbsolutePassed { get; set; }
        public bool RelativePassed { get; set; }
        public string Verdict { get; set; }
    }

    /// <summary>What every element of one series says, derived from ALL its elements (not from the decisive one).</summary>
    public sealed class SeriesSummary
    {
        public int ElementCount { get; set; }
        public int NotPassCount { get; set; }
        public int InformationalCount { get; set; }
        public string NativeEncoding { get; set; }
        public Dictionary<string, int> ByOutcome { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ByStatus { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ProposedCases { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> OwnerDecisions { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> TechnicalBlockers { get; set; } = new Dictionary<string, int>();

        /// <summary>The first not-pass element indices, for orientation; every element is in the elements file.</summary>
        public List<int> FirstNotPass { get; set; } = new List<int>();
    }

    public sealed class ReportResult
    {
        public string QuantityId { get; set; }
        public string ObjectKey { get; set; }
        public string Outcome { get; set; }
        public string Status { get; set; }
        public int Rank { get; set; }
        public string Reason { get; set; }
        public string Expected { get; set; }
        public string Native { get; set; }
        public ReportComparison Comparison { get; set; }
        public string Proposed { get; set; }
        public string OwnerDecision { get; set; }
        public string TechnicalBlocker { get; set; }
        public string AcceptedBy { get; set; }
        public bool Informational { get; set; }
        public string Identity { get; set; }
        public string Alignment { get; set; }
        public string Transformation { get; set; }
        public bool TransformationVerified { get; set; }
        public string Interpretation { get; set; }
        public SeriesSummary Series { get; set; }
    }

    public sealed class ReportIdentity
    {
        public string Space { get; set; }
        public int Zone { get; set; }
        public string Status { get; set; }
        public string Carrier { get; set; }
        public string Evidence { get; set; }
        public List<string> Surfaces { get; set; } = new List<string>();
        public List<string> Apertures { get; set; } = new List<string>();
        public List<int> UnmatchedSurfaces { get; set; } = new List<int>();
    }

    public sealed class ReadBackReport
    {
        public string Schema { get; set; } = "s08-readback-report/1";
        public string FixtureId { get; set; }
        public string Purpose { get; set; }
        public string RuleSet { get; set; }
        public string CalibrationRules { get; set; }
        public string CalibrationRulesSha256 { get; set; }
        public Dictionary<string, string> Inputs { get; set; } = new Dictionary<string, string>();

        /// <summary>The run verdict: the gate status, made at least FAIL by a native object SAM never stated and at least BLOCKED by a D-FIX criterion.</summary>
        public string Status { get; set; }
        public int Rank { get; set; }
        public int ExitCode { get; set; }
        public List<string> StatusReasons { get; set; } = new List<string>();

        /// <summary>The unchanged S07 gate over the gate inputs (decisive element per series), default rule set.</summary>
        public string Gate { get; set; }
        public string GateProvisional { get; set; }
        public string Coverage { get; set; }
        public int RequiredCount { get; set; }
        public List<string> MissingComparisons { get; set; } = new List<string>();
        public int UnrequestedCount { get; set; }
        public bool Provisional { get; set; }

        /// <summary>Counts over the gate inputs (one per scalar, one per series).</summary>
        public Dictionary<string, int> ResultsByOutcome { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ResultsByStatus { get; set; } = new Dictionary<string, int>();

        /// <summary>Counts over EVERY scalar result and EVERY series element, so coexisting cases are never hidden.</summary>
        public Dictionary<string, int> ElementsByOutcome { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ElementsByStatus { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ProposedCases { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> OwnerDecisions { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> TechnicalBlockers { get; set; } = new Dictionary<string, int>();
        public int InformationalMismatches { get; set; }
        public List<string> NotPass { get; set; } = new List<string>();
        public string ElementsFile { get; set; }
        public List<ReportResult> Results { get; set; } = new List<ReportResult>();
        public List<ReportResult> T3DFieldChecks { get; set; } = new List<ReportResult>();
        public List<ReportIdentity> Identity { get; set; } = new List<ReportIdentity>();
        public List<int> UnmatchedZones { get; set; } = new List<int>();
        public List<string> Supporting { get; set; } = new List<string>();
        public List<string> NativeExtras { get; set; } = new List<string>();
        public List<string> FixtureCriteria { get; set; } = new List<string>();
        public List<string> Infrastructure { get; set; } = new List<string>();

        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        /// <summary>The verdict when no comparison could run (reader or export infrastructure failure).</summary>
        public static ReadBackReport InfrastructureFailure(string fixtureId, IEnumerable<string> problems)
        {
            ReadBackReport result = new ReadBackReport { FixtureId = fixtureId, Status = "Infrastructure", Rank = 0, ExitCode = ReadBackExit.Infrastructure };
            result.Infrastructure.AddRange(problems);
            result.StatusReasons.Add("infrastructure failure: no engineering verdict was formed");
            return result;
        }

        public static ReadBackReport Create(ReadBackComparison comparison, string purpose, string elementsFile)
        {
            ReadBackReport r = new ReadBackReport
            {
                FixtureId = comparison.Expected.FixtureId,
                Purpose = purpose,
                RuleSet = comparison.Configuration.RuleSet.ToString(),
                CalibrationRules = comparison.Rules.SourcePath ?? "(none)",
                CalibrationRulesSha256 = comparison.Rules.SourceSha256,
                Gate = comparison.Gate.Status.ToString(),
                GateProvisional = comparison.GateProvisional.Status.ToString(),
                Coverage = comparison.Gate.Coverage.ToString(),
                RequiredCount = comparison.Required.Count,
                MissingComparisons = comparison.Gate.MissingComparisons.Select(x => x.ToString()).ToList(),
                UnrequestedCount = comparison.Gate.Unrequested.Count,
                Provisional = comparison.Gate.Provisional,
                ElementsFile = elementsFile,
                Supporting = comparison.Supporting.ToList(),
                NativeExtras = comparison.NativeExtras.ToList(),
                FixtureCriteria = comparison.Expected.FixtureCriteria.ToList(),
                UnmatchedZones = comparison.Identity.UnmatchedZones.ToList()
            };

            // The run verdict: the gate, never relaxed; native extras and D-FIX criteria only make it stricter.
            InputEquivalence.GateStatus status = comparison.Gate.Status;
            r.StatusReasons.Add("gate: " + comparison.Gate);
            if (comparison.NativeExtras.Count > 0 && (int)status > (int)InputEquivalence.GateStatus.Fail)
            {
                status = InputEquivalence.GateStatus.Fail;
                r.StatusReasons.Add("native objects SAM never stated: " + string.Join("; ", comparison.NativeExtras));
            }

            if (comparison.Expected.FixtureCriteria.Count > 0 && (int)status > (int)InputEquivalence.GateStatus.BlockedOwnerDecision)
            {
                status = InputEquivalence.GateStatus.BlockedOwnerDecision;
                r.StatusReasons.Add("D-FIX criteria: " + string.Join("; ", comparison.Expected.FixtureCriteria));
            }

            r.Status = status.ToString();
            r.Rank = (int)status;
            r.ExitCode = ReadBackExit.Of(status);

            Tally(r, comparison.Results, comparison.Series);
            r.T3DFieldChecks = comparison.T3DFieldChecks.Select(Item).ToList();

            foreach (ZoneMatch zone in comparison.Identity.Zones)
            {
                r.Identity.Add(new ReportIdentity
                {
                    Space = ReadBackComparison.Keys.Space(zone.SpaceGuid),
                    Zone = zone.ZoneIndex,
                    Status = zone.Status.ToString(),
                    Carrier = zone.CarrierAgrees == null ? "absent" : zone.CarrierAgrees == true ? "agrees" : "CONTRADICTS",
                    Evidence = zone.Evidence,
                    Surfaces = zone.Surfaces.Select(x => x.PanelGuid.ToString("D") + " -> surface " + x.SurfaceIndex + " " + x.Status + " (" + x.Evidence + ")").ToList(),
                    Apertures = zone.Apertures.Select(x => x.ApertureGuid.ToString("D") + " -> pane " + string.Join(",", x.PaneSurfaces) + " frame " + string.Join(",", x.FrameSurfaces) + " " + x.Status + " (" + x.Evidence + ")").ToList(),
                    UnmatchedSurfaces = zone.UnmatchedSurfaces.ToList()
                });
            }

            return r;
        }

        /// <summary>
        /// Lists every gate input and tallies outcomes, statuses, proposed cases, owner decisions, technical blockers and
        /// informational mismatches over EVERY scalar result and EVERY series element. A series' <see cref="SeriesResult.GateResult"/>
        /// carries the decisive element's metadata only; these tallies are what keeps a non-decisive G case or blocker visible.
        /// The gate itself is not touched.
        /// </summary>
        public static void Tally(ReadBackReport r, IEnumerable<QuantityResult> results, IEnumerable<SeriesRecord> seriesRecords)
        {
            Dictionary<string, SeriesRecord> series = (seriesRecords ?? Enumerable.Empty<SeriesRecord>()).ToDictionary(x => x.QuantityId + "\u0001" + x.ObjectKey, x => x);
            foreach (QuantityResult result in results)
            {
                series.TryGetValue(result.Definition?.Id + "\u0001" + result.Input?.ObjectKey, out SeriesRecord record);
                ReportResult item = Item(result);
                Increment(r.ResultsByOutcome, result.Outcome.ToString());
                Increment(r.ResultsByStatus, result.Status.ToString());
                if (result.GatesFixture && !result.IsPass)
                {
                    r.NotPass.Add(result.Status + " " + result.Definition?.Id + " " + result.Input?.ObjectKey);
                }

                IEnumerable<QuantityResult> elements = record == null ? new[] { result } : record.Result.Elements;
                if (record != null)
                {
                    item.Series = Summary(record);
                }

                foreach (QuantityResult element in elements)
                {
                    Increment(r.ElementsByOutcome, element.Outcome.ToString());
                    Increment(r.ElementsByStatus, element.Status.ToString());
                    if (element.Proposed != null) Increment(r.ProposedCases, element.Proposed);
                    if (element.OwnerDecisionNamed != null) Increment(r.OwnerDecisions, element.OwnerDecisionNamed);
                    if (element.TechnicalBlockerNamed != null) Increment(r.TechnicalBlockers, element.TechnicalBlockerNamed);
                    if (element.IsInformational) r.InformationalMismatches++;
                }

                r.Results.Add(item);
            }
        }

        private static SeriesSummary Summary(SeriesRecord record)
        {
            SeriesSummary s = new SeriesSummary { ElementCount = record.Result.Elements.Count, NotPassCount = record.Result.NotPassCount, NativeEncoding = record.NativeEncoding };
            for (int i = 0; i < record.Result.Elements.Count; i++)
            {
                QuantityResult e = record.Result.Elements[i];
                Increment(s.ByOutcome, e.Outcome.ToString());
                Increment(s.ByStatus, e.Status.ToString());
                if (e.Proposed != null) Increment(s.ProposedCases, e.Proposed);
                if (e.OwnerDecisionNamed != null) Increment(s.OwnerDecisions, e.OwnerDecisionNamed);
                if (e.TechnicalBlockerNamed != null) Increment(s.TechnicalBlockers, e.TechnicalBlockerNamed);
                if (e.IsInformational) s.InformationalCount++;
                if (!e.IsPass && s.FirstNotPass.Count < 24) s.FirstNotPass.Add(i);
            }

            return s;
        }

        private static ReportResult Item(QuantityResult result)
        {
            QuantityInput input = result.Input;
            return new ReportResult
            {
                QuantityId = result.Definition?.Id,
                ObjectKey = input?.ObjectKey,
                Outcome = result.Outcome.ToString(),
                Status = result.Status.ToString(),
                Rank = (int)result.Status,
                Reason = result.Reason,
                Expected = input?.Expected?.ToString(),
                Native = input?.Native?.ToString(),
                Comparison = result.Comparison == null ? null : new ReportComparison { Difference = result.Comparison.Difference, RelativeDifference = result.Comparison.RelativeDifference, AbsolutePassed = result.Comparison.AbsolutePassed, RelativePassed = result.Comparison.RelativePassed, Verdict = result.Comparison.Verdict.ToString() },
                Proposed = result.Proposed,
                OwnerDecision = result.OwnerDecisionNamed,
                TechnicalBlocker = result.TechnicalBlockerNamed,
                AcceptedBy = result.AcceptedBy,
                Informational = result.IsInformational,
                Identity = input?.Identity.ToString(),
                Alignment = input?.Alignment.ToString(),
                Transformation = input?.TransformationId,
                TransformationVerified = input?.TransformationVerified ?? false,
                Interpretation = input?.Interpretation.ToString()
            };
        }

        private static void Increment(Dictionary<string, int> dictionary, string key)
        {
            dictionary.TryGetValue(key, out int count);
            dictionary[key] = count + 1;
        }

        /// <summary>Every element of every series, one CSV row each, gzip-compressed: the complete evidence behind each series result.</summary>
        public static void WriteElements(ReadBackComparison comparison, string path)
        {
            using (FileStream file = File.Create(path))
            using (GZipStream gzip = new GZipStream(file, CompressionLevel.Optimal))
            using (StreamWriter writer = new StreamWriter(gzip, new UTF8Encoding(false)))
            {
                writer.WriteLine("quantity,object,index,expected,native,outcome,status,informational,proposed,ownerDecision,technicalBlocker");
                foreach (SeriesRecord record in comparison.Series)
                {
                    for (int i = 0; i < record.Result.Elements.Count; i++)
                    {
                        QuantityResult e = record.Result.Elements[i];
                        writer.WriteLine(string.Join(",", Csv(record.QuantityId), Csv(record.ObjectKey), i.ToString(ci), Csv(e.Input?.Expected?.ToString()), Csv(e.Input?.Native?.ToString()), e.Outcome, e.Status, e.IsInformational ? "1" : "0", Csv(e.Proposed), Csv(e.OwnerDecisionNamed), Csv(e.TechnicalBlockerNamed)));
                    }
                }
            }
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>A short, human-readable summary for the console and the PR record.</summary>
        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("S08 read-back " + FixtureId + " (" + Purpose + "): " + Status + " (rank " + Rank + ", exit " + ExitCode + ")");
            foreach (string reason in StatusReasons) sb.AppendLine("  " + reason);
            sb.AppendLine("  gate " + Gate + " | provisional-rules gate " + GateProvisional + " | coverage " + Coverage + " (" + RequiredCount + " required, " + MissingComparisons.Count + " missing, " + UnrequestedCount + " unrequested)");
            sb.AppendLine("  results by status: " + string.Join(", ", ResultsByStatus.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value)));
            sb.AppendLine("  elements by outcome: " + string.Join(", ", ElementsByOutcome.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value)));
            sb.AppendLine("  proposed cases (all elements): " + (ProposedCases.Count == 0 ? "none" : string.Join("; ", ProposedCases.Select(x => x.Key + " x" + x.Value))));
            sb.AppendLine("  owner decisions (all elements): " + OwnerDecisions.Count + "; technical blockers: " + TechnicalBlockers.Count + "; informational mismatches: " + InformationalMismatches);
            foreach (string item in NotPass.Take(60)) sb.AppendLine("  NOT PASS " + item);
            if (NotPass.Count > 60) sb.AppendLine("  ... " + (NotPass.Count - 60) + " more in report.json");
            return sb.ToString();
        }
    }
}
