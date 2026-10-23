// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SAM.Analytical.Tas.DirectT3D.Validation.Results
{
    /// <summary>
    /// The S10 modes. Each licensed read runs in its own process and opens its files read-only:
    /// <list type="bullet">
    /// <item><c>results-read &lt;tsd&gt; &lt;tbd&gt; &lt;outDir&gt;</c>: the independent reference read (TSD arrays, TBD sizing,
    /// thermostat and calendar) to <c>tsd-result.json</c> and <c>tbd-snapshot.json</c>.</item>
    /// <item><c>results-subject &lt;model.json&gt; &lt;tsd&gt; &lt;b1a.json&gt; &lt;out.json&gt;</c>: the subject: the saved SAM
    /// model's results, the production model-level converter run on the same TSD, and the B1a document.</item>
    /// <item><c>results-compare &lt;readDir&gt; &lt;subject.json&gt; &lt;outDir&gt; [rules=&lt;path&gt;]</c>: COM-free reference and
    /// comparison; exit 0 PASS, 10 FAIL, 11 BLOCKED.</item>
    /// <item><c>results-negatives &lt;readDir&gt; &lt;subject.json&gt; &lt;outDir&gt; [rules=&lt;path&gt;] [expect=PASS]</c>: COM-free
    /// negative controls on copies; exit 0 only when every case fails or blocks as intended.</item>
    /// </list>
    /// Exit 64 = usage, 70 = infrastructure (a read that failed, a file that changed).
    /// </summary>
    public static class ResultsRun
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        public static int Run(string[] args)
        {
            switch (args[0])
            {
                case "results-read":
                    return args.Length == 4 ? Read(args[1], args[2], args[3]) : Usage();
                case "results-subject":
                    return args.Length == 5 ? Subject(args[1], args[2], args[3], args[4]) : Usage();
                case "results-compare":
                    return args.Length >= 4 ? Compare(args[1], args[2], args[3], Option(args, "rules")) : Usage();
                case "results-negatives":
                    return args.Length >= 4 ? Negatives(args[1], args[2], args[3], Option(args, "rules"), Option(args, "expect")) : Usage();
                default:
                    return Usage();
            }
        }

        private static int Usage()
        {
            Console.WriteLine("modes: results-read <tsd> <tbd> <outDir> | results-subject <model.json> <tsd> <b1a.json> <out.json> | results-compare <readDir> <subject.json> <outDir> [rules=<path>] | results-negatives <readDir> <subject.json> <outDir> [rules=<path>] [expect=PASS]");
            return ReadBackExit.Usage;
        }

        private static string Option(string[] args, string name)
        {
            return args.Skip(4).Where(x => x.StartsWith(name + "=", StringComparison.Ordinal)).Select(x => x.Substring(name.Length + 1)).LastOrDefault();
        }

        private static int Read(string tsd, string tbd, string outDir)
        {
            string tsdOut = Path.Combine(outDir, "tsd-result.json"), tbdOut = Path.Combine(outDir, "tbd-snapshot.json");
            if (File.Exists(tsdOut) || File.Exists(tbdOut))
            {
                Console.WriteLine("ERROR: refusing to overwrite an earlier read in " + outDir);
                return ReadBackExit.Usage;
            }

            Directory.CreateDirectory(outDir);
            TsdResultSnapshot tsdSnapshot = TsdResultRead.Read(tsd);
            SnapshotJson.Write(tsdSnapshot, tsdOut);
            NativeSnapshot tbdSnapshot = TbdReadBack.Read(tbd, null);
            if (tbdSnapshot.Reader.TbdSha256Before != tbdSnapshot.Reader.TbdSha256After)
            {
                tbdSnapshot.Errors.Add("the TBD changed during the read");
            }

            SnapshotJson.Write(tbdSnapshot, tbdOut);

            Console.WriteLine("TSD " + tsdSnapshot.Sha256Before + " -> " + tsdSnapshot.Sha256After + ", zones " + tsdSnapshot.Zones.Count + ", design-day data sets " + tsdSnapshot.DesignDatasets.Count + ", errors " + tsdSnapshot.Errors.Count);
            Console.WriteLine("TBD " + tbdSnapshot.Reader.TbdSha256Before + " -> " + tbdSnapshot.Reader.TbdSha256After + ", zones " + tbdSnapshot.Zones.Count + ", errors " + tbdSnapshot.Errors.Count);
            foreach (string error in tsdSnapshot.Errors.Concat(tbdSnapshot.Errors)) Console.WriteLine("ERROR: " + error);
            return tsdSnapshot.Errors.Count == 0 && tbdSnapshot.Errors.Count == 0 ? ReadBackExit.Pass : ReadBackExit.Infrastructure;
        }

        private static int Subject(string modelJson, string tsd, string b1a, string output)
        {
            if (File.Exists(output))
            {
                Console.WriteLine("ERROR: refusing to overwrite " + output);
                return ReadBackExit.Usage;
            }

            ResultSubjectSnapshot subject = new ResultSubjectSnapshot
            {
                ProcessId = Process.GetCurrentProcess().Id,
                StartedUtc = DateTime.UtcNow.ToString("o", ci),
                ModelJsonPath = Path.GetFullPath(modelJson),
                ModelJsonSha256 = SnapshotJson.Sha256(modelJson),
                TsdPath = Path.GetFullPath(tsd),
                B1aPath = Path.GetFullPath(b1a),
                B1aSha256 = SnapshotJson.Sha256(b1a),
            };

            AnalyticalModel model = File.Exists(modelJson) ? Core.Convert.ToSAM<AnalyticalModel>(modelJson)?.FirstOrDefault() : null;
            if (model == null) subject.Errors.Add("the saved model could not be read: " + modelJson);
            else ResultSubject.Extract(subject, model);

            if (!File.Exists(b1a)) subject.Errors.Add("B1a missing: " + b1a);
            else subject.B1a = ResultSubject.ParseB1a(File.ReadAllText(b1a));

            // The production model-level converter, on the same TSD, as benchmark-tas ran it (never a stub: it hangs TSD.exe).
            subject.TsdSha256Before = SnapshotJson.Sha256(tsd);
            if (!File.Exists(tsd) || new FileInfo(tsd).Length < TsdReadBack.StubThreshold)
            {
                subject.Errors.Add("TSD missing or a stub: " + tsd);
            }
            else if (model != null)
            {
                subject.ModelResult = ResultSubject.ModelResult(global::SAM.Analytical.Tas.Convert.ToSAM_AnalyticalModelSimulationResult(tsd, model));
            }

            subject.TsdSha256After = SnapshotJson.Sha256(tsd);
            if (subject.TsdSha256After != subject.TsdSha256Before) subject.Errors.Add("the TSD changed during the subject capture");
            if (subject.B1a.ParseError != null) subject.Errors.Add("B1a: " + subject.B1a.ParseError);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            SnapshotJson.Write(subject, output);
            Console.WriteLine("subject: spaces " + subject.Spaces.Count + ", model result " + (subject.ModelResult?.Present == true ? subject.ModelResult.UnitsState : "absent") + ", B1a spaces " + subject.B1a.Spaces.Count + ", errors " + subject.Errors.Count);
            foreach (string error in subject.Errors) Console.WriteLine("ERROR: " + error);
            return subject.Errors.Count == 0 ? ReadBackExit.Pass : ReadBackExit.Infrastructure;
        }

        private static CalibrationRules Rules(string path)
        {
            return CalibrationRules.Load(path ?? Path.Combine(AppContext.BaseDirectory, "calibration-rules.json"));
        }

        private static int Compare(string readDir, string subjectPath, string outDir, string rulesPath)
        {
            TsdResultSnapshot tsd = SnapshotJson.Read<TsdResultSnapshot>(Path.Combine(readDir, "tsd-result.json"));
            NativeSnapshot tbd = SnapshotJson.Read<NativeSnapshot>(Path.Combine(readDir, "tbd-snapshot.json"));
            ResultSubjectSnapshot subject = SnapshotJson.Read<ResultSubjectSnapshot>(subjectPath);
            ResultReferenceModel reference = ResultReference.Build(tsd, tbd, Rules(rulesPath));
            ResultComparison comparison = ResultCompare.Run(reference, subject);

            Directory.CreateDirectory(outDir);
            SnapshotJson.Write(reference, Path.Combine(outDir, "reference.json"));
            ResultReport.Write(outDir, reference, comparison);
            Console.WriteLine("S10 " + comparison.Verdict + ": rows " + comparison.Rows.Count + ", PASS " + comparison.Pass + ", FAIL " + comparison.Fail + ", BLOCKED " + comparison.Blocked);
            return comparison.ExitCode;
        }

        private static int Negatives(string readDir, string subjectPath, string outDir, string rulesPath, string expect)
        {
            ResultSubjectSnapshot subject = SnapshotJson.Read<ResultSubjectSnapshot>(subjectPath);
            foreach (KeyValuePair<string, string> file in new Dictionary<string, string> { [subject.ModelJsonPath] = subject.ModelJsonSha256, [subject.B1aPath] = subject.B1aSha256 })
            {
                if (SnapshotJson.Sha256(file.Key) != file.Value)
                {
                    Console.WriteLine("ERROR: " + file.Key + " no longer has the sha256 the subject recorded (" + file.Value + ")");
                    return ReadBackExit.Infrastructure;
                }
            }

            ResultInputs inputs = new ResultInputs
            {
                TsdJson = File.ReadAllText(Path.Combine(readDir, "tsd-result.json")),
                TbdJson = File.ReadAllText(Path.Combine(readDir, "tbd-snapshot.json")),
                Rules = Rules(rulesPath),
                ModelJson = File.ReadAllText(subject.ModelJsonPath),
                B1aJson = File.ReadAllText(subject.B1aPath),
                ModelResultJson = JsonSerializer.Serialize(subject.ModelResult, SnapshotJson.Options),
            };

            List<NegativeOutcome> outcomes = ResultNegatives.Run(inputs, expect);
            Directory.CreateDirectory(outDir);
            foreach (NegativeOutcome outcome in outcomes)
            {
                string directory = Path.Combine(outDir, outcome.Case);
                Directory.CreateDirectory(directory);
                ResultCase copy = outcome.Copy;
                if (copy != null && outcome.Applicable)
                {
                    if (copy.TsdChanged) SnapshotJson.Write(copy.Tsd, Path.Combine(directory, "tsd-result.mutated.json"));
                    if (copy.ModelChanged) File.WriteAllText(Path.Combine(directory, "model.mutated.json"), Core.Convert.ToString(copy.Model));
                    if (copy.B1aChanged) File.WriteAllText(Path.Combine(directory, "b1a.mutated.json"), copy.B1a.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                    if (copy.ModelResultChanged) SnapshotJson.Write(copy.ModelResult, Path.Combine(directory, "model-result.mutated.json"));
                }

                if (outcome.Comparison != null)
                {
                    ResultReferenceModel reference = ResultReference.Build(copy.Tsd, copy.Tbd, inputs.Rules);
                    ResultReport.Write(directory, reference, outcome.Comparison);
                }
            }

            SnapshotJson.Write(outcomes, Path.Combine(outDir, "negatives.json"));
            File.WriteAllText(Path.Combine(outDir, "negatives.md"), ResultReport.Negatives(outcomes));
            bool all = outcomes.All(x => x.AsIntended);
            foreach (NegativeOutcome outcome in outcomes) Console.WriteLine(outcome.Case + ": " + (outcome.Applicable ? outcome.Verdict : "n/a") + (outcome.AsIntended ? " as intended" : " NOT AS INTENDED"));
            return all ? ReadBackExit.Pass : ReadBackExit.Fail;
        }
    }
}
