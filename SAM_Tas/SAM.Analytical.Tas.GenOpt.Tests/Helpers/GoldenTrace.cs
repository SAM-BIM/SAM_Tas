// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Math;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SAM.Analytical.Tas.GenOpt.Tests.Helpers
{
    /// <summary>
    /// A GenOpt 3.1.1 golden trace from the SAM repository (SAM/SAM.Tests/Golden/GenOpt, recorded from real Java
    /// GenOpt by SAM.Tests.GenOptOracle), expressed as a SAM_Tas <see cref="GenOptDocument"/>. It is replayed end to
    /// end through the native adapter, TasGenExecuteObjectiveEvaluator and the stub TasGenExecute.
    /// <para>
    /// The SAM checkout is the sibling folder, the same layout the HintPath references and CI use.
    /// </para>
    /// </summary>
    public sealed class GoldenTrace
    {
        private JsonElement root;

        public string Name { get; private set; }

        public string Algorithm { get; private set; }

        /// <summary>Why the case cannot be written with the SAM_Tas GenOpt objects, or null when it can.</summary>
        public string NotExpressible { get; private set; }

        public static string Directory_Golden
        {
            get
            {
                DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory != null && !System.IO.File.Exists(Path.Combine(directory.FullName, "SAM_Tas.sln")))
                {
                    directory = directory.Parent;
                }

                if (directory?.Parent == null)
                {
                    throw new DirectoryNotFoundException("SAM_Tas.sln not found above " + AppContext.BaseDirectory);
                }

                return Path.Combine(directory.Parent.FullName, "SAM", "SAM", "SAM.Tests", "Golden", "GenOpt");
            }
        }

        public static IEnumerable<string> Names()
        {
            return Directory.GetFiles(Directory_Golden, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, StringComparer.Ordinal);
        }

        public static IEnumerable<string> ExpressibleNames() => Names().Where(n => Load(n).NotExpressible == null);

        public static GoldenTrace Load(string name)
        {
            GoldenTrace result = new GoldenTrace { Name = name };
            result.root = JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(Directory_Golden, name + ".json"))).RootElement;
            result.Algorithm = result.Case.GetProperty("algorithm").GetString();
            result.NotExpressible = result.Expressibility();
            return result;
        }

        private JsonElement Case => root.GetProperty("case");

        private List<string> Keywords => Case.GetProperty("algorithmKeywords").EnumerateArray().Select(x => x.GetString()).ToList();

        private static string KeywordValue(string keyword) => keyword.Split('=')[1].Trim().TrimEnd(';');

        private string Expressibility()
        {
            foreach (JsonElement parameter in Case.GetProperty("parameters").EnumerateArray())
            {
                if (parameter.GetProperty("min").ValueKind == JsonValueKind.Null || parameter.GetProperty("max").ValueKind == JsonValueKind.Null)
                {
                    return "unbounded parameter (NumberParameter always writes Min and Max)";
                }
            }

            switch (Algorithm)
            {
                case "GPSHookeJeeves":
                    return null;
                case "GPSCoordinateSearch":
                    return "GPSCoordinateSearch (the Java writer adds Seed/NumberOfInitialPoint; refused, D3)";
                case "GoldenSection":
                    string keyword = Keywords.SingleOrDefault();
                    if (keyword == null || !keyword.StartsWith("AbsDiffFunction", StringComparison.Ordinal))
                    {
                        return "GoldenSectionAlgorithm only writes AbsDiffFunction";
                    }

                    return GenOptNumber.WriterText(double.Parse(KeywordValue(keyword), CultureInfo.InvariantCulture)).Contains("E")
                        ? "AbsDiffFunction would be written in exponent notation (refused, D2)"
                        : null;
                default:
                    return "algorithm " + Algorithm;
            }
        }

        /// <summary>The case as the SAM_Tas Java-route objects; the script is the stub spec.</summary>
        public GenOptDocument CreateDocument(string workspace)
        {
            GenOptDocument document = new GenOptDocument(workspace);
            foreach (JsonElement parameter in Case.GetProperty("parameters").EnumerateArray())
            {
                document.AddParameter(new NumberParameter
                {
                    Name = parameter.GetProperty("name").GetString(),
                    Initial = Number(parameter.GetProperty("ini").GetString()),
                    Min = Number(parameter.GetProperty("min").GetString()),
                    Max = Number(parameter.GetProperty("max").GetString()),
                    Step = Number(parameter.GetProperty("step").GetString()),
                });
            }

            document.Algorithm = CreateAlgorithm();
            document.OptimizationSettings.MaxIterations = Case.GetProperty("maxIte").GetInt32();
            document.OptimizationSettings.MaxEqualResults = Case.GetProperty("maxEqualResults").GetInt32();

            JsonElement function = Case.GetProperty("function");
            List<string> outputs = function.GetProperty("outputs").EnumerateArray().Select(x => x.GetString()).ToList();
            foreach (string output in outputs)
            {
                document.AddObjective(output);
            }

            Dictionary<string, string> modes = new Dictionary<string, string>();
            foreach (JsonElement simulation in function.GetProperty("failAtSimulation").EnumerateArray())
            {
                modes[simulation.GetInt32().ToString(CultureInfo.InvariantCulture)] = "errorFile";
            }

            foreach (JsonElement simulation in function.GetProperty("failOnceAtSimulation").EnumerateArray())
            {
                modes[simulation.GetInt32().ToString(CultureInfo.InvariantCulture) + "a1"] = "errorFile";
            }

            document.AddScript(TestFolder.StubScript(
                function.GetProperty("kind").GetString(),
                function.GetProperty("center").EnumerateArray().Select(x => x.GetDouble()).ToArray(),
                function.GetProperty("weight").EnumerateArray().Select(x => x.GetDouble()).ToArray(),
                function.GetProperty("offset").GetDouble(),
                function.GetProperty("quantum").GetDouble(),
                outputs,
                modes));
            return document;
        }

        private Algorithm CreateAlgorithm()
        {
            if (Algorithm == "GoldenSection")
            {
                return new GoldenSectionAlgorithm { AbsDiffFunction = Number(KeywordValue(Keywords.Single())) };
            }

            double Value(string key) => Number(KeywordValue(Keywords.Single(w => w.StartsWith(key + " ", StringComparison.Ordinal))));
            GPSHookeJeevesAlgorithm result = new GPSHookeJeevesAlgorithm
            {
                MeshSizeDivider = Value("MeshSizeDivider"),
                InitialMeshSizeExponent = Value("InitialMeshSizeExponent"),
                MeshSizeExponentIncrement = Value("MeshSizeExponentIncrement"),
                NumberOfStepReduction = Value("NumberOfStepReduction"),
            };
            return result;
        }

        /// <summary>
        /// The case's command-file text as a double. The text the Java writer then produces must read back (GenOpt
        /// arithmetic) to what Java GenOpt read from the original text; otherwise the replay would not be like-for-like.
        /// </summary>
        private static double Number(string text)
        {
            double value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            double viaWriter = GenOptNumber.Read(GenOptNumber.WriterText(value));
            double original = GenOptNumber.Read(text);
            if (BitConverter.DoubleToInt64Bits(viaWriter) != BitConverter.DoubleToInt64Bits(original))
            {
                throw new InvalidOperationException("'" + text + "' does not survive the Java writer: " + viaWriter.ToString("R", CultureInfo.InvariantCulture));
            }

            return value;
        }

        /// <summary>Every difference between the native run and the Java trace (empty when bit-exact).</summary>
        public List<string> Compare(OptimisationResult result)
        {
            List<string> differences = new List<string>();
            CompareRows("all", root.GetProperty("all"), result.Entries, true, differences);
            CompareRows("main", root.GetProperty("main"), result.MainIterations, false, differences);

            List<string> termination = root.GetProperty("termination").EnumerateArray().Select(x => x.GetString()).ToList();
            OptimisationOutcome expected = ExpectedOutcome(termination);
            if (expected != result.Outcome)
            {
                differences.Add("outcome: java " + expected + ", native " + result.Outcome);
            }

            int retries = termination.Count(l => l.Contains("Try to evaluate simulation a second time.", StringComparison.Ordinal));
            if (retries != result.Retries)
            {
                differences.Add("retries: java " + retries + ", native " + result.Retries);
            }

            List<double> footer = root.GetProperty("footer").EnumerateArray().Select(x => x.GetString())
                .Where(l => l.Contains(':') && !l.Contains("***"))
                .Select(l => JavaDouble(l.Substring(l.LastIndexOf(':') + 1).Trim()))
                .ToList();
            List<double> interval = result.Interval == null
                ? new List<double>()
                : new List<double> { result.Interval.Lower, result.Interval.Upper, result.Interval.MidPoint, result.Interval.Length, result.Interval.NormalisedLength };
            if (footer.Count != interval.Count || footer.Where((v, i) => !SameBits(v, interval[i])).Any())
            {
                differences.Add("footer differs");
            }

            return differences;
        }

        private static void CompareRows(string listing, JsonElement expected, IReadOnlyList<OptimisationTraceEntry> actual, bool includeSub, List<string> differences)
        {
            List<JsonElement> rows = expected.EnumerateArray().ToList();
            if (rows.Count != actual.Count)
            {
                differences.Add(listing + ": java " + rows.Count + " rows, native " + actual.Count);
            }

            for (int i = 0; i < System.Math.Min(rows.Count, actual.Count); i++)
            {
                JsonElement e = rows[i];
                OptimisationTraceEntry a = actual[i];
                List<double> f = e.GetProperty("f").EnumerateArray().Select(x => JavaDouble(x.GetString())).ToList();
                List<double> x = e.GetProperty("x").EnumerateArray().Select(v => JavaDouble(v.GetString())).ToList();
                bool same = e.GetProperty("simulation").GetInt32() == a.Simulation
                    && e.GetProperty("mainIteration").GetInt32() == a.MainIteration
                    && (!includeSub || e.GetProperty("subIteration").GetInt32() == a.SubIteration)
                    && f.Count == a.Outputs.Count && f.Select((v, j) => SameBits(v, a.Outputs[j])).All(b => b)
                    && x.Count == a.Coordinates.Count && x.Select((v, j) => SameBits(v, a.Coordinates[j])).All(b => b);
                if (!same)
                {
                    differences.Add(listing + " row " + i + ": java sim " + e.GetProperty("simulation").GetInt32() + " x=" + string.Join(",", x) + " f=" + string.Join(",", f)
                        + " | native sim " + a.Simulation + " x=" + string.Join(",", a.Coordinates) + " f=" + string.Join(",", a.Outputs));
                    return;
                }
            }
        }

        private static OptimisationOutcome ExpectedOutcome(List<string> termination)
        {
            string text = string.Join("\n", termination);
            if (text.StartsWith("Optimization completed successfully.", StringComparison.Ordinal))
            {
                return OptimisationOutcome.Success;
            }

            if (text.Contains("Maximum number of iteration exceeded.", StringComparison.Ordinal))
            {
                return OptimisationOutcome.MaximumSimulationsReached;
            }

            if (text.Contains("Nullspace in line search", StringComparison.Ordinal))
            {
                return OptimisationOutcome.Nullspace;
            }

            if (text.Contains("Following error was found", StringComparison.Ordinal))
            {
                return OptimisationOutcome.EvaluationFailed;
            }

            return OptimisationOutcome.AlgorithmError;
        }

        private static double JavaDouble(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }
}
