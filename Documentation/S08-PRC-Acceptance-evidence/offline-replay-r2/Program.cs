// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.InputEquivalence;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System.IO.Compression;
// S08 PR-C revision 2: replay the 20 persisted acc1 comparisons with the fixed comparator and compare with the committed reports.
var repo = @"C:\Users\michal\Documents\GitHub\SAM-BIM\SAM_Tas";
var rules = Path.Combine(repo, @"SAM_Tas\SAM.Analytical.Tas.DirectT3D.Validation\ReadBack\calibration-rules.json");
var acc = @"C:\TasOut\s08c\acc1";
var output = args[0];
int bad = 0, n = 0;
var runs = new[] { "s08-fix-box", "s08-fix-two", "s08-fix-box-repeat" }.Select(x => Path.Combine(acc, x)).Concat(Directory.GetDirectories(acc, "neg-*").OrderBy(x => x));
foreach (var run in runs)
foreach (var set in new[] { GateRuleSet.Approved, GateRuleSet.ProvisionalProposed })
{
    var name = Path.GetFileName(run); var label = set == GateRuleSet.Approved ? "approved" : "provisional";
    var report = ReadBackCompare.Run(run, rules, name, name.StartsWith("neg-") ? "replay" : "S08 PR-C equivalence fixture (acceptance)", set, Path.Combine(output, name, label));
    var section = name.StartsWith("neg-") ? "negatives" : "runs";
    using var input = File.OpenRead(Path.Combine(repo, "Documentation", "S08-PRC-Acceptance-evidence", section, name, "compare-" + label, "report.json.gz"));
    using var gzip = new GZipStream(input, CompressionMode.Decompress);
    var original = System.Text.Json.JsonSerializer.Deserialize<ReadBackReport>(gzip, SnapshotJson.Options);
    bool equal = report.Status == original.Status && report.ExitCode == original.ExitCode && report.NotPass.Order().SequenceEqual(original.NotPass.Order())
        && System.Text.Json.JsonSerializer.Serialize(report.Results, SnapshotJson.Options) == System.Text.Json.JsonSerializer.Serialize(original.Results, SnapshotJson.Options);
    n++; if (!equal) bad++;
    Console.WriteLine($"{name} {label}: {report.Status}/{report.ExitCode}, required={report.RequiredCount}, results_match_committed={equal}");
}
Console.WriteLine($"{n} comparisons replayed, {bad} differ from the committed reports");
return bad == 0 ? 0 : 1;
