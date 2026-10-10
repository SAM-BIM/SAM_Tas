// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// The SHA-256 (lower-case hex) of every top-level Tas file of <paramref name="folder"/> that TasGenExecute
        /// discovers (<see cref="NativeGenOptWorkspace.IsTasFile"/>: T3D, TBD, TPD, TSD, TWD), by file name
        /// (case-insensitive). "Apply best design" compares them with the run's (<see cref="TasModelRunner.SourceHashes"/>)
        /// to refuse files that changed after the optimisation ran. Only read.
        /// </summary>
        public static Dictionary<string, string> TasFileHashes(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException("The Tas project folder does not exist: '" + folder + "'.");
            }

            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly).Where(NativeGenOptWorkspace.IsTasFile).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                result[Path.GetFileName(path)] = FileHash(path);
            }

            return result;
        }

        /// <summary>The SHA-256 of a file, lower-case hex.</summary>
        public static string FileHash(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return FileHash(stream);
            }
        }

        /// <summary>The SHA-256 of a stream from its current position to its end, lower-case hex.</summary>
        public static string FileHash(Stream stream)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(stream).Select(x => x.ToString("x2")));
            }
        }
    }
}
