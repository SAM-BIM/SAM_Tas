using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace SAM.Analytical.Tas.TPD
{
    // Lightweight per-call timing recorder, mirrors the WorkflowCalculator instrumentation pattern.
    // Each Step("name") closes the previous interval and starts a new one. WriteCsv() emits a CSV
    // next to the TPD file with one row per step plus a TOTAL row. Used to find hotspots inside
    // Convert.ToTPD on large models; safe to leave threaded through code with a null instance.
    internal sealed class TPDProfiler
    {
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly List<string> stepOrder = new List<string>();
        private readonly Dictionary<string, double> totalsByStep = new Dictionary<string, double>();
        private string currentStepName;

        // Accumulates elapsed time per step name. Calling Step("Liquid systems") inside a loop with
        // multiple plantrooms sums up each pass under the same row, which is what we want for the CSV.
        public void Step(string description)
        {
            FinalizeCurrentStep();
            currentStepName = description;
            if (description != null && !totalsByStep.ContainsKey(description))
            {
                stepOrder.Add(description);
                totalsByStep[description] = 0.0;
            }
            stopwatch.Restart();
        }

        public void FinalizeCurrentStep()
        {
            if (currentStepName == null)
            {
                return;
            }

            stopwatch.Stop();
            totalsByStep[currentStepName] = totalsByStep[currentStepName] + stopwatch.Elapsed.TotalMilliseconds;
            currentStepName = null;
        }

        public void WriteCsv(string path_TPD)
        {
            FinalizeCurrentStep();

            if (stepOrder.Count == 0 || string.IsNullOrWhiteSpace(path_TPD))
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(path_TPD);
                string fileName = Path.GetFileNameWithoutExtension(path_TPD);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                {
                    return;
                }

                string path = Path.Combine(directory, fileName + ".timing.csv");
                List<string> lines = new List<string> { "step,milliseconds" };
                double total = 0.0;
                foreach (string name in stepOrder)
                {
                    double value = totalsByStep.TryGetValue(name, out double v) ? v : 0.0;
                    string safeName = name?.Replace("\"", "\"\"") ?? string.Empty;
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "\"{0}\",{1:F1}", safeName, value));
                    total += value;
                }
                lines.Add(string.Format(CultureInfo.InvariantCulture, "\"TOTAL\",{0:F1}", total));
                File.WriteAllLines(path, lines);
            }
            catch
            {
                // Best-effort instrumentation: never break the workflow if writing the CSV fails.
            }
        }
    }
}
