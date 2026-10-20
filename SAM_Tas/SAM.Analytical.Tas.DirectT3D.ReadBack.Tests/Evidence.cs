// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.InputEquivalence;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// The committed licensed evidence (Documentation/S08-PRB-ReadBack-evidence): real persisted-input snapshots of the
    /// synthetic calibration runs, unpacked into a disposable directory so a test can replay or mutate them offline.
    /// </summary>
    public static class Evidence
    {
        public static string Root
        {
            get
            {
                DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, "Documentation", "S08-PRB-ReadBack-evidence");
                    if (Directory.Exists(candidate)) return candidate;
                    directory = directory.Parent;
                }

                throw new DirectoryNotFoundException("Documentation/S08-PRB-ReadBack-evidence not found above " + TestContext.CurrentContext.TestDirectory);
            }
        }

        public static string Rules => Path.Combine(TestContext.CurrentContext.TestDirectory, "calibration-rules.json");

        /// <summary>A fresh copy of one committed run folder, decompressed.</summary>
        public static string Unpack(string run)
        {
            string source = Path.Combine(Root, "runs", run);
            string target = Path.Combine(Path.GetTempPath(), "s08b-tests", run + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".gz", StringComparison.Ordinal))
                {
                    using (FileStream input = File.OpenRead(file))
                    using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
                    using (FileStream output = File.Create(Path.Combine(target, name.Substring(0, name.Length - 3))))
                    {
                        gzip.CopyTo(output);
                    }
                }
                else
                {
                    File.Copy(file, Path.Combine(target, name));
                }
            }

            return target;
        }

        public static NativeSnapshot Snapshot(string directory)
        {
            return SnapshotJson.Read<NativeSnapshot>(Path.Combine(directory, ReadBackFiles.Snapshot));
        }

        public static ExpectedModel Expected(string directory, string fixtureId)
        {
            AnalyticalModel model = Core.Convert.ToSAM<AnalyticalModel>(Path.Combine(directory, ReadBackFiles.FrozenModel)).First();
            Weather.WeatherData weather = Core.Convert.ToSAM<Weather.WeatherData>(Path.Combine(directory, ReadBackFiles.FrozenWeather)).First();
            return ReadBackExpected.Build(model, weather, fixtureId);
        }

        public static ReadBackComparison Compare(string directory, NativeSnapshot snapshot, string fixtureId = "test", GateRuleSet ruleSet = GateRuleSet.Interim, CalibrationRules rules = null)
        {
            return ReadBackComparison.Run(Expected(directory, fixtureId), snapshot, rules ?? CalibrationRules.Load(Rules), ruleSet);
        }

        public static QuantityResult Result(ReadBackComparison comparison, string quantityId, string keyEndsWith)
        {
            return comparison.Results.Single(x => x.Definition.Id == quantityId && x.Input.ObjectKey.EndsWith(keyEndsWith, StringComparison.Ordinal));
        }
    }
}
