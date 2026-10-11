// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>What one <see cref="TasModelDesignChange"/> did to the Tas file it was written to.</summary>
    public sealed class TasModelAppliedValue
    {
        public TasModelAppliedValue(TasModelDesignChange change, string before, string after, bool changed, string detail = null)
        {
            Change = change ?? throw new ArgumentNullException(nameof(change));
            Before = before;
            After = after;
            Changed = changed;
            Detail = detail;
        }

        public TasModelDesignChange Change { get; }

        /// <summary>The file the change is written to: "TBD" or "TPD".</summary>
        public string File => Change.IsBuilding ? "TBD" : "TPD";

        /// <summary>The value (or glazing construction) read before writing, as invariant text at full precision.</summary>
        public string Before { get; }

        /// <summary>The value (or glazing construction) read back after writing, as invariant text at full precision.</summary>
        public string After { get; }

        /// <summary>False when the file already held the best value (nothing was written for this change).</summary>
        public bool Changed { get; }

        /// <summary>For example "12 of 24 hours" or "pane on 3 elements"; null when there is nothing to add.</summary>
        public string Detail { get; }
    }

    /// <summary>
    /// How "Apply best design" changed the Tas project (<see cref="TasModelDesignApplier.Apply"/>): every change with
    /// what was read before and after, the files replaced, and the folder that holds the originals.
    /// </summary>
    public sealed class TasModelApplyResult
    {
        public TasModelApplyResult(string projectFolder, string workFolder, string backupFolder, IEnumerable<string> filesReplaced, IEnumerable<TasModelAppliedValue> values)
        {
            ProjectFolder = projectFolder;
            WorkFolder = workFolder;
            BackupFolder = backupFolder;
            FilesReplaced = (filesReplaced ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
            Values = (values ?? Enumerable.Empty<TasModelAppliedValue>()).ToList().AsReadOnly();
        }

        public string ProjectFolder { get; }

        /// <summary>This application's own folder under the project (<see cref="TasModelDesignApplier.WorkFolderName"/>); null when nothing was written.</summary>
        public string WorkFolder { get; }

        /// <summary>The copies of the original files taken before any was replaced; null when nothing was replaced.</summary>
        public string BackupFolder { get; }

        /// <summary>The project files replaced (file names); empty when every value was already the best one.</summary>
        public IReadOnlyList<string> FilesReplaced { get; }

        /// <summary>One entry per change, in the definition's order.</summary>
        public IReadOnlyList<TasModelAppliedValue> Values { get; }
    }

    /// <summary>
    /// "Apply best design" could not change the Tas project. <see cref="ProjectChanged"/> says whether any project file
    /// is not as it was: false in every case but a failed restore after a partial replacement, which the message then
    /// spells out file by file.
    /// </summary>
    public sealed class TasModelApplyException : Exception
    {
        public TasModelApplyException(string message, bool projectChanged = false, string workFolder = null, Exception innerException = null)
            : base(message, innerException)
        {
            ProjectChanged = projectChanged;
            WorkFolder = workFolder;
        }

        /// <summary>True when a project file could not be restored: see the message and <see cref="WorkFolder"/>.</summary>
        public bool ProjectChanged { get; }

        /// <summary>The application's work folder (staging copies and the backup of the originals), when one was made.</summary>
        public string WorkFolder { get; }
    }
}
