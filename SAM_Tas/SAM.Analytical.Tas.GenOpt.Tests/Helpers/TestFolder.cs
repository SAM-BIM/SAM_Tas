// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SAM.Analytical.Tas.GenOpt.Tests.Helpers
{
    /// <summary>
    /// A short, unique temporary folder for one test. It is deleted on dispose. It also provides the path of the stub
    /// TasGenExecute and helpers that write a stub spec as the document script.
    /// </summary>
    public sealed class TestFolder : IDisposable
    {
        public TestFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAMGenOptT", Guid.NewGuid().ToString("N").Substring(0, 10));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        /// <summary>The protocol stand-in, copied next to the test assembly by its ProjectReference.</summary>
        public static string StubExecutable => System.IO.Path.Combine(AppContext.BaseDirectory, "StubTasGenExecute.exe");

        public string Combine(params string[] parts)
        {
            List<string> all = new List<string> { Path };
            all.AddRange(parts);
            return System.IO.Path.Combine(all.ToArray());
        }

        /// <summary>Creates a sub-folder and returns its path.</summary>
        public string Folder(string name)
        {
            string path = Combine(name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>The JSON stub spec used as the document's script (see StubTasGenExecute.Program.Spec).</summary>
        public static string StubScript(
            string kind = "quadratic",
            double[] center = null,
            double[] weight = null,
            double offset = 0,
            double quantum = 1,
            IEnumerable<string> outputs = null,
            IDictionary<string, string> modes = null,
            IDictionary<string, int> sleepMs = null,
            string format = "R",
            bool datePrefix = true)
        {
            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["kind"] = kind,
                ["center"] = center ?? new double[0],
                ["weight"] = weight ?? new double[0],
                ["offset"] = offset,
                ["quantum"] = quantum,
                ["outputs"] = outputs ?? new[] { "Result" },
                ["modes"] = modes ?? new Dictionary<string, string>(),
                ["sleepMs"] = sleepMs ?? new Dictionary<string, int>(),
                ["format"] = format,
                ["datePrefix"] = datePrefix,
            });
        }
    }
}
