// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// The result-less TSD refusal, stated once so the workflow and every caller agree on it.
        /// <para>
        /// A successful <c>Modify.AddResults</c> on a model with spaces returns a <see cref="SpaceSimulationResult"/> per
        /// TAS zone and load type (measured on the licensed box model: two, heating and cooling, beside the surface and
        /// cluster results). A TSD that passed every file check and still yields none - a stub or a result-less file TAS
        /// left behind - carries no zone results, and accepting it would persist a "simulated" model with nothing in it.
        /// The caller fails closed.
        /// </para>
        /// <para>
        /// Returns the refusal sentence, or null when there is nothing to refuse: the model has no spaces (nothing to
        /// expect), or at least one <see cref="SpaceSimulationResult"/> came back. Reading the TSD stays in
        /// <see cref="WorkflowCalculator"/>; what lives here is the decision, which a test can pin without licensed TAS.
        /// </para>
        /// </summary>
        /// <param name="count_Spaces">How many spaces the model being given results has.</param>
        /// <param name="results">What <c>Modify.AddResults</c> returned; null when it read nothing.</param>
        /// <returns>The refusal when a model with spaces received no zone results; otherwise null.</returns>
        public static string ZoneResultsRefusal(int count_Spaces, IEnumerable<Core.Result> results)
        {
            if (count_Spaces <= 0)
                return null;

            int count = 0;
            if (results != null)
            {
                foreach (Core.Result result in results)
                {
                    if (result is SpaceSimulationResult)
                        return null;

                    if (result != null)
                        count++;
                }
            }

            return string.Format(
                "Adding Results: the TSD gave no zone results for a model with {0} space(s) ({1}), so it is not evidence of a successful simulation. Nothing was saved.",
                count_Spaces,
                results == null ? "the results reader returned nothing" : string.Format("{0} other result(s)", count));
        }
    }
}
