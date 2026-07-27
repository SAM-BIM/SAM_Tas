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
        /// Runs the TAS workflow with a message-pumped progress form that carries a Cancel button. Cancellation
        /// is cooperative and between-step (see <see cref="WorkflowCalculator.CancellationToken"/>): it aborts
        /// before the next step but cannot interrupt the in-flight TAS COM simulate/sizing call. The optional
        /// <paramref name="externalCancellationToken"/> lets a caller (e.g. WorkflowTBD's own COM pre-step) share
        /// one cancel across its stage and this one. On cancellation the method returns null and sets
        /// <paramref name="cancelled"/> true; on any other failure it returns null with <paramref name="cancelled"/>
        /// false (indistinguishable from the previous behaviour for non-cancelled callers).
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
            using (ProgressForm progressForm = new("Workflow"))
            {
                progressForm.Cancellable = true;
                progressForm.CancelRequested += (s, e) => cancellationTokenSource.Cancel();

                WorkflowCalculator workflowCalculator = new(workflowSettings)
                {
                    CancellationToken = cancellationTokenSource.Token
                };

                workflowCalculator.StepsCounted += (s, e) =>
                {
                    progressForm.Max = e.Count;
                };

                workflowCalculator.Updating += (s, e) =>
                {
                    progressForm.Update(e.Description);
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
    }
}
