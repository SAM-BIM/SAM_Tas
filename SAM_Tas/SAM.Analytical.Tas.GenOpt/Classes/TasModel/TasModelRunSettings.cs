// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// Where and how a "tas-model" definition runs on this computer. Local settings, never part of the portable
    /// definition (as <see cref="TasExecutionSettings"/> for "tas-script"; there is no script path: the script is generated).
    /// </summary>
    public sealed class TasModelRunSettings
    {
        public TasModelRunSettings()
        {
        }

        public TasModelRunSettings(string projectFolder, string runsFolder = null, string tasGenExecutePath = null)
        {
            ProjectFolder = projectFolder;
            RunsFolder = runsFolder;
            TasGenExecutePath = tasGenExecutePath;
        }

        /// <summary>The Tas project folder (TBD, TSD, TPD). Each run works on a copy; it is never changed.</summary>
        public string ProjectFolder { get; set; }

        /// <summary>Parent of the run folders; null for <c>SAM_NativeGenOpt</c> in the project folder. Keep it short.</summary>
        public string RunsFolder { get; set; }

        /// <summary>TasGenExecute.exe; null for the installed one (<see cref="Query.TasGenOptExecutePath"/>).</summary>
        public string TasGenExecutePath { get; set; }

        /// <summary>The glazing pool a glazing choice's options come from (the same pool the catalogue was built with).</summary>
        public IEnumerable<TasGlazingSystem> GlazingPool { get; set; }

        /// <summary>The glazing option filter; null for the defaults (the same filter the catalogue was built with).</summary>
        public TasGlazingFilter GlazingFilter { get; set; }

        /// <summary>
        /// The project's inventory when the caller has it already (the catalogue step); null to read it from
        /// <see cref="ProjectFolder"/> (licensed Tas).
        /// </summary>
        public TasModelInventory Inventory { get; set; }
    }
}
