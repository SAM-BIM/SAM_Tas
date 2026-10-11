// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// The name suffixes of the TBD copies Energy Simulation's sizing leaves beside the project's TBD (SAM_Tas
        /// <c>Query.Sizing</c>: <c>&lt;name&gt;_Uncapped.tbd</c> before the air glass, <c>&lt;name&gt;_HDDCDD.tbd</c> before the
        /// oversizing factors; the project's <c>&lt;name&gt;.tbd</c> is the one simulated).
        /// </summary>
        public static readonly IReadOnlyList<string> SizingCopySuffixes = Array.AsReadOnly(new[] { "_Uncapped", "_HDDCDD" });

        /// <summary>
        /// The top-level Tas files of <paramref name="folder"/> split into the project's files and Energy Simulation's
        /// sizing copies. A TBD is a sizing copy only when its name is <c>&lt;name&gt;_Uncapped</c> or
        /// <c>&lt;name&gt;_HDDCDD</c> and <c>&lt;name&gt;.tbd</c> is in the folder too (case-insensitive); one without its
        /// primary is not assumed to be a copy (<see cref="TasProjectFiles.UnmatchedSizingNames"/>). The inventory, the run
        /// snapshot, the run's file hashes and "Apply best design" all use this one split, so the sizing copies are never
        /// opened, copied, hashed or changed. Only lists the folder.
        /// </summary>
        public static TasProjectFiles TasProjectFiles(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException("The Tas project folder does not exist: '" + folder + "'.");
            }

            List<string> names = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(NativeGenOptWorkspace.IsTasFile)
                .Select(Path.GetFileName)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            HashSet<string> all = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            List<string> files = new List<string>();
            List<string> sizingCopies = new List<string>();
            List<string> unmatched = new List<string>();
            foreach (string name in names)
            {
                string primary = SizingCopyPrimary(name);
                if (primary == null)
                {
                    files.Add(name);
                }
                else if (all.Contains(primary))
                {
                    sizingCopies.Add(name);
                }
                else
                {
                    files.Add(name);
                    unmatched.Add(name);
                }
            }

            return new TasProjectFiles(folder, files, sizingCopies, unmatched);
        }

        /// <summary>The primary TBD a TBD named like a sizing copy would be a copy of (<c>A_HDDCDD.tbd</c> → <c>A.tbd</c>); null for any other name.</summary>
        internal static string SizingCopyPrimary(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            if (!string.Equals(extension, ".tbd", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string name = Path.GetFileNameWithoutExtension(fileName);
            foreach (string suffix in SizingCopySuffixes)
            {
                if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return name.Substring(0, name.Length - suffix.Length) + extension;
                }
            }

            return null;
        }
    }
}
