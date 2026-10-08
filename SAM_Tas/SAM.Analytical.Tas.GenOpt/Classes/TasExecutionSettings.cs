// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// Where and how an Optimisation Definition runs on this computer: the Tas project folder, the Tas script, the runs
    /// folder and TasGenExecute. These are local settings. They are never part of the portable definition, and applying
    /// or pasting a definition never changes them.
    /// </summary>
    public sealed class TasExecutionSettings
    {
        public TasExecutionSettings()
        {
        }

        public TasExecutionSettings(string projectFolder, string scriptPath, string runsFolder = null, string tasGenExecutePath = null)
        {
            ProjectFolder = projectFolder;
            ScriptPath = scriptPath;
            RunsFolder = runsFolder;
            TasGenExecutePath = tasGenExecutePath;
        }

        public TasExecutionSettings(TasExecutionSettings tasExecutionSettings)
        {
            if (tasExecutionSettings == null)
            {
                return;
            }

            ProjectFolder = tasExecutionSettings.ProjectFolder;
            ScriptPath = tasExecutionSettings.ScriptPath;
            RunsFolder = tasExecutionSettings.RunsFolder;
            TasGenExecutePath = tasExecutionSettings.TasGenExecutePath;
        }

        /// <summary>The Tas project folder (T3D/TBD/TPD/TSD/TWD files). Each run works on a copy; it is never changed.</summary>
        public string ProjectFolder { get; set; }

        /// <summary>The C# Tas script TasGenExecute runs for each simulation.</summary>
        public string ScriptPath { get; set; }

        /// <summary>Parent of the run folders; null for <c>SAM_NativeGenOpt</c> in the project folder.</summary>
        public string RunsFolder { get; set; }

        /// <summary>TasGenExecute.exe; null for the installed one (<see cref="Query.TasGenOptExecutePath"/>).</summary>
        public string TasGenExecutePath { get; set; }
    }
}
