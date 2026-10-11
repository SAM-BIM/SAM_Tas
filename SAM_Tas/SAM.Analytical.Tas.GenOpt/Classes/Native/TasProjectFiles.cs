// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// The top-level Tas files of a project folder as the optimisation sees them (<see cref="Query.TasProjectFiles"/>):
    /// the project's own files, and the sizing copies Energy Simulation leaves beside its TBD, which the optimisation
    /// never opens, copies, hashes or changes.
    /// </summary>
    public sealed class TasProjectFiles
    {
        internal TasProjectFiles(string folder, IList<string> files, IList<string> sizingCopies, IList<string> unmatchedSizingNames)
        {
            Folder = folder;
            Files = new ReadOnlyCollection<string>(files);
            SizingCopies = new ReadOnlyCollection<string>(sizingCopies);
            UnmatchedSizingNames = new ReadOnlyCollection<string>(unmatchedSizingNames);
        }

        /// <summary>The folder, as given.</summary>
        public string Folder { get; }

        /// <summary>
        /// File names of the project's Tas files (<see cref="NativeGenOptWorkspace.IsTasFile"/>), sizing copies left out,
        /// in case-insensitive order. A TBD named like a sizing copy whose primary is missing stays here (and in
        /// <see cref="UnmatchedSizingNames"/>): it is not known to be a copy.
        /// </summary>
        public IReadOnlyList<string> Files { get; }

        /// <summary>
        /// File names of Energy Simulation's sizing copies (<c>&lt;name&gt;_Uncapped.tbd</c>, <c>&lt;name&gt;_HDDCDD.tbd</c>,
        /// SAM_Tas <c>Query.Sizing</c>) whose primary <c>&lt;name&gt;.tbd</c> is in the folder, in case-insensitive order.
        /// </summary>
        public IReadOnlyList<string> SizingCopies { get; }

        /// <summary>
        /// File names of TBDs named like a sizing copy whose primary TBD is not in the folder: the project's TBD cannot be
        /// identified from them, so the "tas-model" engine refuses the folder.
        /// </summary>
        public IReadOnlyList<string> UnmatchedSizingNames { get; }
    }
}
