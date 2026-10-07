// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Math;
using System;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// How a native run ended, read from the SAM.Math kernel's <see cref="OptimisationResult"/> and the caller's
    /// cancellation, by the rules every consumer shares (the Grasshopper component since PR4, SAM_UI since PR5; held here
    /// since PR6). Wording and layout stay with each consumer.
    /// <list type="bullet">
    /// <item>Success, the simulation limit and a golden-section nullspace stop are normal ends (<see cref="Completed"/>);
    /// cancellation, an evaluation failure and every other outcome are not.</item>
    /// <item>A run the user asked to stop is never successful, even if the kernel finished first: its result is
    /// <see cref="Withheld"/>.</item>
    /// <item>Only a successful run has a best point (<see cref="Best(OptimisationResult)"/>) and, for golden section, a
    /// final <see cref="Interval"/>.</item>
    /// </list>
    /// </summary>
    public sealed class NativeGenOptOutcome
    {
        /// <param name="nativeGenOptRun">The run <see cref="GenOptDocument.RunNative"/> returned.</param>
        /// <param name="cancelRequested">The user asked to stop before the run returned.</param>
        public NativeGenOptOutcome(NativeGenOptRun nativeGenOptRun, bool cancelRequested)
            : this(nativeGenOptRun?.Result, cancelRequested)
        {
        }

        /// <param name="result">The kernel result.</param>
        /// <param name="cancelRequested">The user asked to stop before the run returned.</param>
        public NativeGenOptOutcome(OptimisationResult result, bool cancelRequested)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Outcome = result.Outcome;
            Completed = IsCompleted(result.Outcome);
            Withheld = Completed && cancelRequested;
            if (Successful)
            {
                BestEntry = Best(result);
                Interval = result.Interval;
            }
        }

        public OptimisationResult Result { get; }

        /// <summary>The kernel's own outcome, also when the result is withheld.</summary>
        public OptimisationOutcome Outcome { get; }

        /// <summary>The kernel ended normally: Success, the simulation limit or a golden-section nullspace stop.</summary>
        public bool Completed { get; }

        /// <summary>The kernel ended normally but the user had asked to stop: no best point is reported.</summary>
        public bool Withheld { get; }

        /// <summary>Ended normally and not withheld: the only case with a best point.</summary>
        public bool Successful => Completed && !Withheld;

        /// <summary>The best point of a successful run (<see cref="Best(OptimisationResult)"/>); otherwise null.</summary>
        public OptimisationTraceEntry BestEntry { get; }

        /// <summary>Golden section's final interval of a successful run; otherwise null.</summary>
        public GoldenSectionInterval Interval { get; }

        /// <summary>Success, MaximumSimulationsReached and Nullspace are normal ends; every other outcome is not.</summary>
        public static bool IsCompleted(OptimisationOutcome optimisationOutcome)
        {
            switch (optimisationOutcome)
            {
                case OptimisationOutcome.Success:
                case OptimisationOutcome.MaximumSimulationsReached:
                case OptimisationOutcome.Nullspace:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The best point: the kernel's reported minimum (pattern search). Golden section reports none, so its best point
        /// is the lowest objective among all entries, the first one on a tie, NaN skipped (<see cref="IsLower"/>; the PR3
        /// acceptance definition). Null when there is no result or no entry with a number.
        /// </summary>
        public static OptimisationTraceEntry Best(OptimisationResult result)
        {
            if (result == null)
            {
                return null;
            }

            if (result.Minimum != null)
            {
                return result.Minimum;
            }

            OptimisationTraceEntry best = null;
            foreach (OptimisationTraceEntry entry in result.Entries)
            {
                if (IsLower(entry, best))
                {
                    best = entry;
                }
            }

            return best;
        }

        /// <summary>
        /// The running "lowest so far" rule: <paramref name="entry"/> replaces <paramref name="lowest"/> when its objective
        /// is a number and strictly lower (or there is no lowest yet), so the first one wins a tie and NaN never wins.
        /// </summary>
        public static bool IsLower(OptimisationTraceEntry entry, OptimisationTraceEntry lowest)
        {
            if (entry == null || double.IsNaN(entry.Objective))
            {
                return false;
            }

            return lowest == null || entry.Objective < lowest.Objective;
        }

        /// <summary>
        /// Why <see cref="GenOptDocument.RunNative"/> refused or failed before the kernel ran, by exception kind:
        /// invalid settings (<see cref="GenOptCompatibilityException"/>), an unsupported algorithm
        /// (<see cref="NotSupportedException"/>), a missing TasGenExecute (with its path) or workspace, otherwise the type
        /// and message.
        /// </summary>
        public static string RefusalMessage(Exception exception)
        {
            switch (exception)
            {
                case GenOptCompatibilityException _:
                    return "Invalid GenOpt settings for the native route: " + exception.Message;

                case NotSupportedException _:
                    return "Not supported by the native route: " + exception.Message;

                case System.IO.FileNotFoundException fileNotFoundException:
                    return exception.Message + " Path: '" + fileNotFoundException.FileName + "'.";

                case System.IO.DirectoryNotFoundException _:
                    return exception.Message;

                default:
                    return "Native optimisation failed (" + exception?.GetType().Name + "): " + exception?.Message;
            }
        }
    }
}
