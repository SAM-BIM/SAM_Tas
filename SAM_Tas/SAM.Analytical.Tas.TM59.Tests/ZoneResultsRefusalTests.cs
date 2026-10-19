// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// S01a, A7. A TSD that passes the file checks and still gives no zone results must not become a "simulated"
    /// model. A successful <c>Modify.AddResults</c> returns a <see cref="SpaceSimulationResult"/> per TAS zone and load
    /// type (licensed box model: heating and cooling, beside 26 surface and 2 cluster results); the workflow refuses
    /// when a model with spaces gets none. Reading the TSD needs TAS, so the decision is what is pinned here.
    /// </summary>
    [TestFixture]
    public class ZoneResultsRefusalTests
    {
        private static SpaceSimulationResult SpaceResult()
        {
            return new SpaceSimulationResult("Office", "SAM.Analytical.Tas", System.Guid.NewGuid().ToString());
        }

        [Test]
        public void A7_SpacesButNoSpaceResult_Refuses()
        {
            List<Core.Result> results = new List<Core.Result>
            {
                new AdjacencyClusterSimulationResult("cluster", "SAM.Analytical.Tas", System.Guid.NewGuid().ToString()),
            };

            string refusal = SAM.Analytical.Tas.Query.ZoneResultsRefusal(1, results);

            Assert.That(refusal, Is.Not.Null);
            Assert.That(refusal, Does.Contain("no zone results for a model with 1 space(s)").And.Contain("1 other result(s)"));
        }

        [Test]
        public void A7_SpacesAndNothingRead_Refuses()
        {
            Assert.That(SAM.Analytical.Tas.Query.ZoneResultsRefusal(3, null), Does.Contain("the results reader returned nothing"));
            Assert.That(SAM.Analytical.Tas.Query.ZoneResultsRefusal(3, new List<Core.Result>()), Does.Contain("0 other result(s)"));
        }

        [Test]
        public void A7_ASpaceResult_IsAccepted()
        {
            List<Core.Result> results = new List<Core.Result>
            {
                new AdjacencyClusterSimulationResult("cluster", "SAM.Analytical.Tas", System.Guid.NewGuid().ToString()),
                SpaceResult(),
            };

            Assert.That(SAM.Analytical.Tas.Query.ZoneResultsRefusal(1, results), Is.Null, "the successful shape must stay successful");
        }

        [Test]
        public void A7_NoSpaces_IsNothingToExpect()
        {
            Assert.That(SAM.Analytical.Tas.Query.ZoneResultsRefusal(0, null), Is.Null);
            Assert.That(SAM.Analytical.Tas.Query.ZoneResultsRefusal(0, new List<Core.Result>()), Is.Null);
        }
    }
}
