// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S10 reference independence: the reference files (the TSD reader, the reference arithmetic, the comparison and the
    /// snapshot model) never call the SAM_Tas code under test. The subject capture (ResultsRun) is the only file that calls
    /// the production converter, by design.
    /// </summary>
    [TestFixture]
    public class S10SourceScanTests
    {
        public static readonly string[] ReferenceFiles = { "TsdResultRead.cs", "ResultReference.cs", "ResultCompare.cs", "ResultSnapshot.cs" };

        public static readonly string[] Forbidden =
        {
            "Convert.ToSAM_", "ValueDictionary", "MaxValueDictionary", "DesignDataDictionary", "ZeroBasedHour", "ZoneResultSeries",
            "Query.AnnualBuildingResult", "Query.UnmetHours", "Query.Compare", "FullYearRefusal", "Core.Tas.Query", "SAMTSDDocument",
            "SAMTBDDocument", "Analytical.Tas.Query", "Analytical.Tas.Convert", "Analytical.Tas.Create", "Analytical.Tas.Modify",
            "using SAM.Analytical.Tas;", "using SAM.Core.Tas;", "Weather.Tas", "TemperatureSetPointDictionary", "DailyValues",
        };

        private static string ResultsDirectory()
        {
            DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "SAM_Tas", "SAM.Analytical.Tas.DirectT3D.Validation", "Results");
                if (Directory.Exists(candidate)) return candidate;
                candidate = Path.Combine(directory.FullName, "SAM.Analytical.Tas.DirectT3D.Validation", "Results");
                if (Directory.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("SAM.Analytical.Tas.DirectT3D.Validation/Results not found above " + TestContext.CurrentContext.TestDirectory);
        }

        /// <summary>Every forbidden token found in the code lines of <paramref name="source"/> (comment lines are not code).</summary>
        public static List<string> Scan(string source)
        {
            List<string> result = new List<string>();
            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                foreach (string token in Forbidden.Where(x => line.Contains(x)))
                {
                    result.Add("line " + (i + 1) + ": " + token);
                }
            }

            return result;
        }

        [Test]
        public void ReferenceFiles_CallNothingOfTheCodeUnderTest()
        {
            string directory = ResultsDirectory();
            foreach (string file in ReferenceFiles)
            {
                string path = Path.Combine(directory, file);
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(Scan(File.ReadAllText(path)), Is.Empty, file);
            }
        }

        [Test]
        public void Scanner_FindsAForbiddenCall()
        {
            string mutated = File.ReadAllText(Path.Combine(ResultsDirectory(), "ResultReference.cs")).Replace("public static ResultReferenceModel Build(", "public static object Cheat(string p) => SAM.Analytical.Tas.Convert.ToSAM_Results(p);\n        public static ResultReferenceModel Build(");

            Assert.That(Scan(mutated), Has.Some.Contains("Convert.ToSAM_"));
        }

        [Test]
        public void SubjectCapture_IsTheOnlyCallerOfTheProductionConverter()
        {
            string directory = ResultsDirectory();
            List<string> callers = Directory.GetFiles(directory, "*.cs").Where(x => File.ReadAllText(x).Contains("ToSAM_AnalyticalModelSimulationResult(")).Select(Path.GetFileName).ToList();

            Assert.That(callers, Is.EqualTo(new[] { "ResultsRun.cs" }));
        }
    }
}
