// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// COM-free: compiled into the licensed harness AND linked into the offline read-back tests.
namespace SAM.Analytical.Tas.DirectT3D.Validation.Results
{
    /// <summary>The S10 report: report.json (everything) and report.md (verdict, calibration, every non-PASS row, coverage).</summary>
    public static class ResultReport
    {
        public sealed class Document
        {
            public string Schema { get; set; } = "s10-result-report/1";
            public string Verdict { get; set; }
            public int ExitCode { get; set; }
            public int Rows { get; set; }
            public int Pass { get; set; }
            public int Fail { get; set; }
            public int Blocked { get; set; }
            public string Mutation { get; set; }
            public string TsdSha256 { get; set; }
            public string TbdSha256 { get; set; }
            public string RulesSha256 { get; set; }
            public List<string> Blockers { get; set; }
            public List<string> SubjectErrors { get; set; }
            public List<CalibrationCheck> Checks { get; set; }
            public List<string> Observations { get; set; }
            public List<ResultRow> Results { get; set; }
        }

        public static Document Of(ResultReferenceModel reference, ResultComparison comparison)
        {
            return new Document
            {
                Verdict = comparison.Verdict,
                ExitCode = comparison.ExitCode,
                Rows = comparison.Rows.Count,
                Pass = comparison.Pass,
                Fail = comparison.Fail,
                Blocked = comparison.Blocked,
                Mutation = comparison.Mutation,
                TsdSha256 = reference.TsdSha256,
                TbdSha256 = reference.TbdSha256,
                RulesSha256 = reference.RulesSha256,
                Blockers = reference.Blockers,
                SubjectErrors = comparison.SubjectErrors,
                Checks = reference.Checks,
                Observations = reference.Observations,
                Results = comparison.Rows,
            };
        }

        public static void Write(string directory, ResultReferenceModel reference, ResultComparison comparison)
        {
            Directory.CreateDirectory(directory);
            SnapshotJson.Write(Of(reference, comparison), Path.Combine(directory, "report.json"));
            File.WriteAllText(Path.Combine(directory, "report.md"), Markdown(reference, comparison));
        }

        public static string Markdown(ResultReferenceModel reference, ResultComparison comparison)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("# S10 Tas results round-trip: " + comparison.Verdict + (comparison.Mutation == null ? string.Empty : " (negative: " + comparison.Mutation + ")"));
            text.AppendLine();
            text.AppendLine("Rows " + comparison.Rows.Count + ": PASS " + comparison.Pass + ", FAIL " + comparison.Fail + ", BLOCKED " + comparison.Blocked + ". Exit " + comparison.ExitCode + ".");
            text.AppendLine("TSD sha256 `" + reference.TsdSha256 + "`, TBD sha256 `" + reference.TbdSha256 + "` (design-day coverage read `" + reference.CoverageSha256 + "`), rules sha256 `" + reference.RulesSha256 + "`.");
            text.AppendLine();
            if (reference.Blockers.Count > 0 || comparison.SubjectErrors.Count > 0)
            {
                text.AppendLine("## Blockers");
                foreach (string blocker in reference.Blockers.Concat(comparison.SubjectErrors)) text.AppendLine("- " + blocker);
                text.AppendLine();
            }

            text.AppendLine("## Calibration (units, signs, interval, shapes, identity)");
            text.AppendLine();
            text.AppendLine("| Check | Status | Answer |");
            text.AppendLine("|---|---|---|");
            foreach (CalibrationCheck check in reference.Checks) text.AppendLine("| " + check.Id + " " + Cell(check.Question) + " | " + check.Status + " | " + Cell(check.Answer) + " |");
            text.AppendLine();
            foreach (CalibrationCheck check in reference.Checks.Where(x => x.Evidence.Count > 0))
            {
                text.AppendLine("- " + check.Id + ": " + string.Join("; ", check.Evidence.Take(12)));
            }

            text.AppendLine();
            List<ResultRow> notPass = comparison.Rows.Where(x => x.Status != RowStatus.Pass).ToList();
            text.AppendLine("## Not PASS (" + notPass.Count + ")");
            text.AppendLine();
            Table(text, notPass.Take(200));
            text.AppendLine();
            text.AppendLine("## Observations (not gated)");
            foreach (string observation in reference.Observations) text.AppendLine("- " + observation);
            text.AppendLine();
            text.AppendLine("## Coverage: every required row");
            text.AppendLine();
            Table(text, comparison.Rows);
            return text.ToString();
        }

        private static void Table(StringBuilder text, IEnumerable<ResultRow> rows)
        {
            text.AppendLine("| Row | Class | Expected | Actual | Status | Reason |");
            text.AppendLine("|---|---|---|---|---|---|");
            foreach (ResultRow row in rows)
            {
                text.AppendLine("| " + Cell(row.Id) + (row.ZoneName == null ? string.Empty : " (" + Cell(row.ZoneName) + ")") + " | " + row.Class + " | " + Cell(row.Expected) + " | " + Cell(row.Actual) + " | " + row.Status.ToString().ToUpperInvariant() + " | " + Cell(row.Reason) + " |");
            }
        }

        public static string Negatives(List<NegativeOutcome> outcomes)
        {
            StringBuilder text = new StringBuilder();
            bool all = outcomes.All(x => x.AsIntended);
            text.AppendLine("# S10 negative controls: " + (all ? "all as intended" : "NOT all as intended"));
            text.AppendLine();
            text.AppendLine("| Case | Applied | Expected | Verdict | FAIL/BLOCKED rows | As intended | Intended rows |");
            text.AppendLine("|---|---|---|---|---|---|---|");
            foreach (NegativeOutcome outcome in outcomes)
            {
                text.AppendLine("| " + outcome.Case + " | " + Cell(outcome.Applicable ? outcome.Applied : "n/a: " + outcome.Applied) + " | " + outcome.ExpectedStatus + " | " + (outcome.Verdict ?? "-") + " | " + outcome.Fail + "/" + outcome.Blocked + " | " + (outcome.AsIntended ? "yes" : "NO") + " | " + Cell(string.Join("; ", outcome.Evidence.Take(4))) + " |");
            }

            return text.ToString();
        }

        private static string Cell(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
