// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace SAM.Analytical.Tas.GenOpt.Tests.StubTasGenExecute
{
    /// <summary>
    /// Stand-in for TasGenExecute.exe (Gate T protocol): <c>StubTasGenExecute "&lt;project&gt;"</c> with the working
    /// directory set to the evaluation folder.
    /// <para>
    /// Instead of a C# script, the project's Script.txt holds a JSON <see cref="Spec"/>: a synthetic objective (the
    /// same expressions as the GenOpt oracle's fake simulator) plus behaviours keyed by simulation number. The number is
    /// taken from the evaluation folder name (<c>NNNN</c> or <c>NNNN-retry</c>). "4" applies to every attempt of
    /// simulation 4; "4a1" applies to its first attempt only.
    /// </para>
    /// <para>
    /// Like TasGenExecute, a missing Script.txt or Variables.txt gives exit code 0xE0434352 and no files. Every call
    /// also writes <c>stub-invocation.txt</c> (arguments and working directory) into the working directory.
    /// </para>
    /// </summary>
    public static class Program
    {
        public sealed class Spec
        {
            public string Kind { get; set; } = "quadratic";
            public double[] Center { get; set; } = new double[0];
            public double[] Weight { get; set; } = new double[0];
            public double Offset { get; set; }
            public double Quantum { get; set; } = 1;
            public List<string> Outputs { get; set; } = new List<string> { "Result" };

            /// <summary>"R" (round-trip, for exact replays) or "G15" (TasGenExecute's 15 significant digits).</summary>
            public string Format { get; set; } = "R";

            /// <summary>Prefix the first output line with a date, as TasGenExecute does ("&lt;date&gt;::Result::v").</summary>
            public bool DatePrefix { get; set; } = true;

            /// <summary>Behaviour by simulation key: errorFile, compileError, exitCode, missingResult, malformedResult,
            /// commaDecimal, appendTwice, whitespaceError.</summary>
            public Dictionary<string, string> Modes { get; set; } = new Dictionary<string, string>();

            /// <summary>Milliseconds to sleep before writing results, by simulation key.</summary>
            public Dictionary<string, int> SleepMs { get; set; } = new Dictionary<string, int>();
        }

        private const int UnhandledClrException = unchecked((int)0xE0434352);

        public static int Main(string[] args)
        {
            string workingDirectory = Directory.GetCurrentDirectory();
            File.WriteAllLines(Path.Combine(workingDirectory, "stub-invocation.txt"), new[] { "argc=" + args.Length.ToString(CultureInfo.InvariantCulture) }.Concat(args.Select(a => "arg=" + a)).Concat(new[] { "cwd=" + workingDirectory }));

            string project = args.Length > 0 ? args[0] : workingDirectory;
            string scriptPath = Path.Combine(project, "Script.txt");
            string variablesPath = Path.Combine(workingDirectory, "Variables.txt");
            if (!File.Exists(scriptPath) || !File.Exists(variablesPath))
            {
                return UnhandledClrException;
            }

            Spec spec = JsonSerializer.Deserialize<Spec>(File.ReadAllText(scriptPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Match match = Regex.Match(Path.GetFileName(workingDirectory), "^([0-9]+)(-retry)?");
            string simulation = match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : "0";
            bool retry = match.Success && match.Groups[2].Success;

            string mode = Lookup(spec.Modes, simulation, retry) ?? "normal";
            int sleep;
            string sleepKey = Key(spec.SleepMs.Keys, simulation, retry);
            if (sleepKey != null && spec.SleepMs.TryGetValue(sleepKey, out sleep))
            {
                Thread.Sleep(sleep);
            }

            List<double> x = File.ReadAllLines(variablesPath)
                .Where(l => l.Trim().Length > 0)
                .Select(l => double.Parse(l.Split(',')[1], NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToList();
            double[] values = Evaluate(spec, x);

            string outputPath = Path.Combine(workingDirectory, "Output.txt");
            string errorPath = Path.Combine(workingDirectory, "Error.txt");
            string date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            switch (mode)
            {
                case "exitCode":
                    return UnhandledClrException;
                case "errorFile":
                    File.WriteAllText(errorPath, date + "::Error: (Line 3) -> \"injected script exception\"");
                    File.WriteAllText(outputPath, string.Empty);
                    break;
                case "compileError":
                    File.WriteAllText(errorPath, "(3,5): error CS1002: ; expected");
                    File.WriteAllText(outputPath, string.Empty);
                    break;
                case "missingResult":
                    File.WriteAllLines(outputPath, Lines(spec, values, date).Skip(1));
                    break;
                case "malformedResult":
                    File.WriteAllLines(outputPath, new[] { date + "::" + spec.Outputs[0] + "::abc" }.Concat(Lines(spec, values, date).Skip(1)));
                    break;
                case "commaDecimal":
                    File.WriteAllLines(outputPath, new[] { date + "::" + spec.Outputs[0] + "::" + values[0].ToString("G15", CultureInfo.GetCultureInfo("pl-PL")) }.Concat(Lines(spec, values, date).Skip(1)));
                    break;
                case "appendTwice":
                    File.WriteAllLines(outputPath, new[] { date + "::" + spec.Outputs[0] + "::999999" }.Concat(Lines(spec, values, date)));
                    break;
                case "whitespaceError":
                    File.WriteAllText(errorPath, "  \r\n");
                    File.WriteAllLines(outputPath, Lines(spec, values, date));
                    break;
                default:
                    File.WriteAllLines(outputPath, Lines(spec, values, date));
                    break;
            }

            File.WriteAllText(Path.Combine(workingDirectory, "stub-finished.txt"), "finished");
            return 0;
        }

        private static string Key(IEnumerable<string> keys, string simulation, bool retry)
        {
            List<string> list = keys.ToList();
            if (!retry && list.Contains(simulation + "a1"))
            {
                return simulation + "a1";
            }

            return list.Contains(simulation) ? simulation : null;
        }

        private static string Lookup(Dictionary<string, string> modes, string simulation, bool retry)
        {
            string key = Key(modes.Keys, simulation, retry);
            return key == null ? null : modes[key];
        }

        private static IEnumerable<string> Lines(Spec spec, double[] values, string date)
        {
            for (int i = 0; i < spec.Outputs.Count; i++)
            {
                string text = spec.Outputs[i] + "::" + values[i].ToString(spec.Format, CultureInfo.InvariantCulture);
                yield return i == 0 && spec.DatePrefix ? date + "::" + text : text;
            }
        }

        /// <summary>The GenOpt oracle's synthetic objective (FunctionSpec); output k &gt; 0 is k times the coordinate sum.</summary>
        private static double[] Evaluate(Spec spec, IReadOnlyList<double> x)
        {
            double Quadratic()
            {
                double f = spec.Offset;
                for (int i = 0; i < x.Count; i++)
                {
                    double d = x[i] - (i < spec.Center.Length ? spec.Center[i] : 0);
                    f += (i < spec.Weight.Length ? spec.Weight[i] : 1) * d * d;
                }

                return f;
            }

            double Linear()
            {
                double f = spec.Offset;
                for (int i = 0; i < x.Count; i++)
                {
                    f += (i < spec.Weight.Length ? spec.Weight[i] : 1) * x[i];
                }

                return f;
            }

            double value;
            switch (spec.Kind)
            {
                case "constant":
                    value = spec.Offset;
                    break;
                case "quadratic":
                    value = Quadratic();
                    break;
                case "quantisedQuadratic":
                    value = Math.Floor(Quadratic() / spec.Quantum) * spec.Quantum;
                    break;
                case "linear":
                    value = Linear();
                    break;
                default:
                    throw new InvalidOperationException("Unknown kind " + spec.Kind);
            }

            double sum = 0;
            for (int i = 0; i < x.Count; i++)
            {
                sum += x[i];
            }

            double[] result = new double[spec.Outputs.Count];
            result[0] = value;
            for (int k = 1; k < result.Length; k++)
            {
                result[k] = k * sum;
            }

            return result;
        }
    }
}
