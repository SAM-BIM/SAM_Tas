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
            {
                // Not a using: the dialog has to be torn down BEFORE the final cancellation check below, and a
                // using would dispose it after. The Cancel button lives on the dialog's own UI thread, so it can
                // be clicked at any instant - including after a check placed here. Checking then disposing only
                // moves that race; disposing then checking closes it, because Dispose closes the form and joins
                // its thread, so afterwards no further CancelRequested can arrive and any in-flight one has
                // already run.
                ProgressFormHost progressFormHost = new("Workflow", 1, true, Analytical.Tas.Query.CancelNote(null));

                try
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
                        progressFormHost.Note = Analytical.Tas.Query.CancelNote(e.Description);
                        progressFormHost.Update(e.Description);
                    };

                    result = workflowCalculator.Calculate(analyticalModel);
                }
                catch (System.OperationCanceledException)
                {
                    cancelled = true;
                    result = null;
                }
                finally
                {
                    progressFormHost.Dispose();
                }

                // Past this point no cancel can be raised, so this observation is final. It catches a click that
                // landed after WorkflowCalculator's own last check - without it the component would report
                // success and emit a model the user had asked it to stop producing.
                if (!cancelled && cancellationTokenSource.IsCancellationRequested)
                {
                    cancelled = true;
                    result = null;
                }
            }

            return result;
        }
    }
}
