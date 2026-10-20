// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence.Tests
{
    /// <summary>
    /// Locks the S07 section 5 tolerances (merged in SAM#216, owner-approved 2026-10-20) so that no later change can
    /// relax one silently. Each row is transcribed from the document: id, relative, absolute, gated, hourly.
    /// </summary>
    [TestFixture]
    public class CatalogTests
    {
        private static IEnumerable<TestCaseData> S07Rows()
        {
            // id, rel, abs, gated, hourlyAligned
            yield return new TestCaseData("1a", 0.005, 0.01, true, false);
            yield return new TestCaseData("1b", 0.005, 0.01, true, false);
            yield return new TestCaseData("2", 0.005, 0.01, true, false);
            yield return new TestCaseData("3a", 1e-5, 1e-6, true, false);
            yield return new TestCaseData("3b", 0.01, 0.005, true, false);
            yield return new TestCaseData("3b.ground", 0.01, 0.005, false, false);
            yield return new TestCaseData("3c.layer", 1e-5, 1e-6, true, false);
            yield return new TestCaseData("3c.U", 0.01, 0.005, true, false);
            yield return new TestCaseData("3c.g", 0.01, 0.001, false, false);
            yield return new TestCaseData("4a.density", 0.001, 0.001, true, false);
            yield return new TestCaseData("4a.people", 0.001, 1e-5, true, false);
            yield return new TestCaseData("4b", 0.005, 0.1, true, false);
            yield return new TestCaseData("4c", null, 0.001, true, false);
            yield return new TestCaseData("5.temperature", null, 0.01, true, true);
            yield return new TestCaseData("5.humidity", null, 0.1, true, true);
            yield return new TestCaseData("6.hourly", null, 1e-4, true, true);
            yield return new TestCaseData("6.eflh", 0.001, null, true, false);
            yield return new TestCaseData("7a", 0.001, 1e-4, true, false);
            yield return new TestCaseData("7b", 0.005, 1e-5, true, false);
            yield return new TestCaseData("8a", 0.001, 1e-6, true, false);
            yield return new TestCaseData("8b", 0.005, 1e-5, true, true);
            yield return new TestCaseData("8d", 0.001, 0.001, true, false);
            yield return new TestCaseData("9", 1e-5, null, true, false);
            yield return new TestCaseData("9.ground", 1e-5, null, false, false);
            yield return new TestCaseData("10", 1e-5, null, true, false);
            yield return new TestCaseData("10.yearDay", null, 0.0, true, false);
        }

        [TestCaseSource(nameof(S07Rows))]
        public void Row_Equals_The_S07_Table(string id, double? relative, double? absolute, bool gated, bool hourly)
        {
            QuantityDefinition definition = QuantityCatalog.Get(id);
            Assert.That(definition.Tolerance.Relative, Is.EqualTo(relative), id + " relative");
            Assert.That(definition.Tolerance.Absolute, Is.EqualTo(absolute), id + " absolute");
            Assert.That(definition.Gated, Is.EqualTo(gated), id + " gated (proposed OD-G2 definition)");
            Assert.That(definition.HourlyAligned, Is.EqualTo(hourly), id + " hour-aligned (S07 section 7)");
            if (!gated)
            {
                Assert.That(definition.NotGatedReason, Is.Not.Null.And.Not.Empty, id + " must say why it is not gated");
            }
        }

        [Test]
        public void The_Catalog_Has_Exactly_The_Locked_Rows()
        {
            Assert.That(QuantityCatalog.All.Count, Is.EqualTo(S07Rows().Count()));
            Assert.That(QuantityCatalog.All.Select(x => x.Id), Is.Unique);
        }

        [Test]
        public void Informational_Quantities_Have_No_Catalogue_Row()
        {
            // S07 gives no tolerance for these, so none is invented: engine U/g, DNI, exhaust, IZAM/TPD, air-panel mixing.
            foreach (string id in new[] { "8c", "8e", "9.dni", "3b.engine", "3c.engine" })
            {
                Assert.That(QuantityCatalog.TryGet(id, out _), Is.False, id);
            }

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => QuantityCatalog.Get("nope"));
        }

        [Test]
        public void The_Hourly_Aligned_Quantities_Are_Exactly_Those_S07_Section_7_Names()
        {
            // "hourly-aligned quantities (5, 6, 7a profile, 8b)": the 7a profile shape is compared as row 6.
            Assert.That(QuantityCatalog.All.Where(x => x.HourlyAligned).Select(x => x.Id), Is.EquivalentTo(new[] { "5.temperature", "5.humidity", "6.hourly", "8b" }));
        }
    }
}
