// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using SAM.Core.Windows.Forms;
using System.Threading;

namespace SAM.Analytical.Grasshopper.Tas
{
    public static partial class Modify
    {
        public static AnalyticalModel RunWorkflow(this AnalyticalModel analyticalModel, WorkflowSettings workflowSettings)
        {
            return RunWorkflow(analyticalModel, workflowSettings, CancellationToken.None, out bool _);
        }

        public static AnalyticalModel RunWorkflow(this AnalyticalModel analyticalModel, WorkflowSettings workflowSettings, out bool cancelled)
        {
            return RunWorkflow(analyticalModel, workflowSettings, CancellationToken.None, out cancelled);
        }

        /// <summary>
        /// Runs the TAS workflow with a progress dialog that carries a Cancel button. Cancellation is
        /// cooperative and between-step (see <see cref="WorkflowCalculator.CancellationToken"/>): it aborts
        /// before the next step but cannot interrupt the in-flight TAS COM simulate/sizing call.
        /// <para>
        /// The dialog runs on its own UI thread (<see cref="ProgressFormHost"/>) rather than on this one. The
        /// workflow blocks this thread for minutes at a time, and Windows ghosts a window whose thread has
        /// stopped pumping and then discards clicks on the ghost — so a Cancel button on this thread's own
        /// form silently loses the click and the run carries on to completion. The job itself stays here; only
        /// the dialog moves, so no TAS COM object changes apartment.
        /// </para>
        /// The optional <paramref name="externalCancellationToken"/> lets a caller (e.g. WorkflowTBD's own COM
        /// pre-step) share one cancel across its stage and this one. On cancellation the method returns null
        /// and sets <paramref name="cancelled"/> true; on any other failure it returns null with
        /// <paramref name="cancelled"/> false (indistinguishable from the previous behaviour for non-cancelled
        /// callers).
        /// </summary>
        public static AnalyticalModel RunWorkflow(this AnalyticalModel analyticalModel, WorkflowSettings workflowSettings, CancellationToken externalCancellationToken, out bool cancelled)
        {
            cancelled = false;

            if (analyticalModel == null)
            {
                return null;
            }

            if (workflowSettings == null)
            {
                workflowSettings = new WorkflowSettings();
            }

            AnalyticalModel result = analyticalModel;
            using (CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken))
            using (ProgressFormHost progressFormHost = new("Workflow", 1, true, CancelNote(null)))
            {
                progressFormHost.CancelRequested += (s, e) => cancellationTokenSource.Cancel();

                WorkflowCalculator workflowCalculator = new(workflowSettings)
                {
                    CancellationToken = cancellationTokenSource.Token
                };

                workflowCalculator.StepsCounted += (s, e) =>
                {
                    progressFormHost.Max = e.Count;
                };

                workflowCalculator.Updating += (s, e) =>
                {
                    progressFormHost.Note = CancelNote(e.Description);
                    progressFormHost.Update(e.Description);
                };

                try
                {
                    result = workflowCalculator.Calculate(analyticalModel);
                }
                catch (System.OperationCanceledException)
                {
                    cancelled = true;
                    result = null;
                }
            }

            return result;
        }

        /// <summary>
        /// Stage names whose TAS COM call runs for minutes and cannot be interrupted once entered. Matched as
        /// case-insensitive substrings of the <c>Updating</c> description raised by
        /// <see cref="WorkflowCalculator"/>.
        /// </summary>
        private static readonly string[] UninterruptibleSteps =
        {
            "Shading",
            "Sizing",
            "Simulating",
            "Importing gbXML",
            "Adding Results",
            "Unmet Hours",
            "Design Loads",
        };

        /// <summary>
        /// The note shown under the progress text. Cancellation is only ever observed between stages, so this
        /// says so plainly, and calls out the long stages (shading, sizing, simulation) where the wait after
        /// clicking Cancel can be minutes rather than seconds.
        /// </summary>
        private static string CancelNote(string description)
        {
            if (!string.IsNullOrWhiteSpace(description))
            {
                foreach (string uninterruptibleStep in UninterruptibleSteps)
                {
                    if (description.IndexOf(uninterruptibleStep, System.StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        return "Cannot cancel during '" + description + "' - this stage may run for several minutes.";
                    }
                }
            }

            return "Cancel takes effect once the current stage finishes - it cannot interrupt one in progress.";
        }
    }
}
