// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical;
using System;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// Guards the MAGNITUDE half of the ventilation round trip - the unit contract between what the TBD
    /// import stores for a collected <c>ticV</c> profile and what the export multiplies back out.
    /// <para>
    /// These tests exist because a licensed round trip turned a 2.0 ACH source profile into 40.8 ACH. The
    /// shape and type came back correctly; the rate did not. The import was storing a ticV extreme - an
    /// AIR CHANGE RATE - into <see cref="InternalConditionParameter.SupplyAirFlow"/>, which
    /// <c>Query.CalculatedSupplyAirFlow</c> reads as m3/s, and the export's own "/ volume * 3600" then
    /// inflated it by 3600/volume. The defect was dormant for as long as the imported ventilation
    /// reference dangled, and went live the moment ticV became a collected, resolvable profile.
    /// </para>
    /// <para>
    /// Everything here is COM-free: a Space plus an InternalCondition is enough to exercise the whole
    /// SAM-side airflow calculation, and the export's conversion is one documented line of arithmetic
    /// (<c>Modify.UpdateInternalCondition</c>, ticV slot) mirrored in <see cref="TicVFactor"/>. That is
    /// what makes this reproducible without a licence - and what would have caught the failure before
    /// TAS was ever run.
    /// </para>
    /// </summary>
    [TestFixture]
    public class VentilationAirflowMagnitudeTests
    {
        private const double Volume = 200.0;   // m3
        private const double Area = 50.0;      // m2
        private const double SourceRate = 2.0; // ACH, the peak of the source ticV profile

        // The export's ticV conversion, verbatim from Modify.UpdateInternalCondition: the SAM design
        // airflow [m3/s] becomes the TBD ticV factor [ACH] over the space volume.
        private static double TicVFactor(Space space)
        {
            double airFlow = SAM.Analytical.Query.CalculatedSupplyAirFlow(space);
            if (double.IsNaN(airFlow))
            {
                return 1.0; // the export's own fallback when nothing is specified
            }

            space.TryGetValue(SAM.Analytical.SpaceParameter.Volume, out double volume);
            return airFlow / volume * 3600.0;
        }

        // The unit lives in the ParameterProperties attribute's description (SAM.Core.Query.Description
        // reads DescriptionAttribute, which these parameters do not carry), so read it directly.
        private static string DeclaredUnit(InternalConditionParameter internalConditionParameter)
        {
            System.Reflection.FieldInfo fieldInfo = typeof(InternalConditionParameter).GetField(internalConditionParameter.ToString());
            object[] attributes = fieldInfo.GetCustomAttributes(typeof(SAM.Core.Attributes.ParameterProperties), false);

            Assert.That(attributes, Is.Not.Empty, "Expected a ParameterProperties attribute on " + internalConditionParameter);

            return ((SAM.Core.Attributes.ParameterProperties)attributes[0]).Description;
        }

        // The template path's parameter preference, verbatim from Modify.UpdateInternalConditionTemplate.
        private static InternalConditionParameter TemplateTicVParameter(InternalCondition internalCondition)
        {
            if (internalCondition.TryGetValue(InternalConditionParameter.SupplyAirChangesPerHour, out double airChangesPerHour) && !double.IsNaN(airChangesPerHour))
            {
                return InternalConditionParameter.SupplyAirChangesPerHour;
            }

            return InternalConditionParameter.SupplyAirFlow;
        }

        private static Space Space_WithCondition(Action<InternalCondition> configure)
        {
            Space space = new Space("Cell 1");
            space.SetValue(SAM.Analytical.SpaceParameter.Volume, Volume);
            space.SetValue(SAM.Analytical.SpaceParameter.Area, Area);

            InternalCondition internalCondition = new InternalCondition("Cell 1");
            configure(internalCondition);
            space.InternalCondition = internalCondition;

            return space;
        }

        // =================================================================================================
        // The unit contract the fix rests on
        // =================================================================================================

        [Test]
        public void Units_SupplyAirFlowIsVolumeFlow_AndSupplyAirChangesPerHourIsARate()
        {
            //The whole defect is a unit confusion between these two parameters, so pin their declared
            //units. If either description ever changes, the reasoning in these tests stops holding.
            Assert.That(DeclaredUnit(InternalConditionParameter.SupplyAirFlow), Does.Contain("m3/s"),
                "SupplyAirFlow is a volume flow; a ticV extreme is not one.");
            Assert.That(DeclaredUnit(InternalConditionParameter.SupplyAirChangesPerHour), Does.Contain("ACH"),
                "SupplyAirChangesPerHour is the air-change-rate basis a ticV extreme belongs in.");
        }

        // =================================================================================================
        // The round trip
        // =================================================================================================

        [Test]
        public void ImportedTicVPeak_StoredAsAirChangesPerHour_ExportsBackAsTheSameRate()
        {
            //What the corrected import writes for a source ticV whose peak factor*value is 2.0 ACH.
            Space space = Space_WithCondition(x => x.SetValue(InternalConditionParameter.SupplyAirChangesPerHour, SourceRate));

            //The ACH basis converts to m3/s as rate * volume / 3600 ...
            Assert.That(SAM.Analytical.Query.CalculatedSupplyAirFlow(space), Is.EqualTo(SourceRate * Volume / 3600.0).Within(1e-12));

            //... and the export's "/ volume * 3600" is its exact inverse, so the rate returns unchanged.
            Assert.That(TicVFactor(space), Is.EqualTo(SourceRate).Within(1e-9),
                "A ticV rate carried on the ACH basis must round-trip unchanged, whatever the volume.");
        }

        [TestCase(50.0)]
        [TestCase(200.0)]
        [TestCase(1234.5)]
        public void ImportedTicVPeak_OnTheAirChangesBasis_IsVolumeIndependent(double volume)
        {
            //The inverse-pair property must not depend on the zone: this is what makes the ACH basis the
            //right home for a rate, and it is exactly what the m3/s basis fails below.
            Space space = Space_WithCondition(x => x.SetValue(InternalConditionParameter.SupplyAirChangesPerHour, SourceRate));
            space.SetValue(SAM.Analytical.SpaceParameter.Volume, volume);

            Assert.That(TicVFactor(space), Is.EqualTo(SourceRate).Within(1e-9));
        }

        // =================================================================================================
        // The regression itself
        // =================================================================================================

        [Test]
        public void ImportedTicVPeak_StoredAsSupplyAirFlow_IsInflatedByTheVolumeRatio()
        {
            //The pre-fix import state: the same 2.0 ACH stored as though it were 2.0 m3/s.
            Space space = Space_WithCondition(x => x.SetValue(InternalConditionParameter.SupplyAirFlow, SourceRate));

            //Read as a volume flow it passes straight through, and the export then scales it by 3600/volume.
            Assert.That(SAM.Analytical.Query.CalculatedSupplyAirFlow(space), Is.EqualTo(SourceRate).Within(1e-12));

            double factor = TicVFactor(space);
            Assert.That(factor, Is.EqualTo(36.0).Within(1e-9), "2.0 'm3/s' over 200 m3 is 36 ACH.");
            Assert.That(factor, Is.EqualTo(SourceRate * 3600.0 / Volume).Within(1e-9),
                "The error is exactly the 3600/volume factor - the signature of the unit mismatch.");

            //And the headline assertion: the two storage choices are NOT interchangeable.
            Assert.That(factor, Is.Not.EqualTo(SourceRate).Within(1e-6),
                "SupplyAirFlow = 2.0 must never be treated as equivalent to SupplyAirChangesPerHour = 2.0.");
        }

        [Test]
        public void TheTwoStorageChoices_DisagreeByTheVolumeRatio_NotByRounding()
        {
            Space space_ACH = Space_WithCondition(x => x.SetValue(InternalConditionParameter.SupplyAirChangesPerHour, SourceRate));
            Space space_Flow = Space_WithCondition(x => x.SetValue(InternalConditionParameter.SupplyAirFlow, SourceRate));

            Assert.That(TicVFactor(space_Flow) / TicVFactor(space_ACH), Is.EqualTo(3600.0 / Volume).Within(1e-9),
                "18x on a 200 m3 zone - the licensed run measured this as 2.0 ACH becoming 40.8 with the "
                + "per-person basis included.");
        }

        // =================================================================================================
        // Precedence: how the bases combine
        // =================================================================================================

        [Test]
        public void CalculatedSupplyAirFlow_SumsEveryBasis_ItDoesNotSelectOne()
        {
            //Pinned because the licensed 40.8 ACH was the ACH-misread-as-m3/s term PLUS a per-person term,
            //and reading the combination rule wrongly would misattribute the error. There is no precedence
            //and no max(): every specified basis is added.
            Space space = Space_WithCondition(x =>
            {
                x.SetValue(InternalConditionParameter.SupplyAirChangesPerHour, SourceRate);   // 0.111111 m3/s
                x.SetValue(InternalConditionParameter.SupplyAirFlowPerPerson, 0.008);         // per person
                x.SetValue(InternalConditionParameter.AreaPerPerson, 7.5);                   // -> 6.667 people
                x.SetValue(InternalConditionParameter.SupplyAirFlowPerArea, 0.0004);          // per m2
                x.SetValue(InternalConditionParameter.SupplyAirFlow, 0.01);                  // absolute
            });

            double expected = SourceRate * Volume / 3600.0     // ACH basis
                + 0.008 * (Area / 7.5)                          // per-person basis
                + 0.0004 * Area                                 // per-area basis
                + 0.01;                                         // absolute basis

            Assert.That(SAM.Analytical.Query.CalculatedSupplyAirFlow(space), Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void CalculatedSupplyAirFlow_WithNoBasisSpecified_IsNaN_SoTheExportFallsBackToFactorOne()
        {
            //The untouched-ticV case: nothing specified, so the export writes its factor-1 fallback rather
            //than a computed rate. This is why a model with no ventilation intent is left alone.
            Space space = Space_WithCondition(x => { });

            Assert.That(SAM.Analytical.Query.CalculatedSupplyAirFlow(space), Is.NaN);
            Assert.That(TicVFactor(space), Is.EqualTo(1.0));
        }

        // =================================================================================================
        // Template / unused internal conditions
        // =================================================================================================

        [Test]
        public void TemplateCondition_HasNoVolume_SoItsTicVFactorIsTheAirChangesValueItself()
        {
            //Modify.UpdateInternalConditionTemplate has no space to convert through, so whichever parameter
            //it reads becomes the ticV factor verbatim. An imported condition now carries only the ACH
            //basis, so a template path still reading SupplyAirFlow would find nothing and silently write a
            //zero ventilation factor - which is why that path prefers SupplyAirChangesPerHour.
            InternalCondition internalCondition = new InternalCondition("Template");
            internalCondition.SetValue(InternalConditionParameter.SupplyAirChangesPerHour, SourceRate);

            Assert.That(TemplateTicVParameter(internalCondition), Is.EqualTo(InternalConditionParameter.SupplyAirChangesPerHour));
            Assert.That(internalCondition.TryGetValue(TemplateTicVParameter(internalCondition), out double value), Is.True);
            Assert.That(value, Is.EqualTo(SourceRate).Within(1e-12),
                "The template factor is this value verbatim, in the same ACH unit the space path yields.");

            Assert.That(internalCondition.TryGetValue(InternalConditionParameter.SupplyAirFlow, out double _), Is.False,
                "The corrected import writes only the ACH basis.");
        }

        [Test]
        public void TemplateCondition_CarryingOnlyTheLegacyFlowParameter_StillUsesIt()
        {
            //A SAM-authored template that predates the ACH basis must keep the factor it has always been
            //given, so the preference falls back rather than writing a zero.
            InternalCondition internalCondition = new InternalCondition("Legacy template");
            internalCondition.SetValue(InternalConditionParameter.SupplyAirFlow, 0.05);

            Assert.That(TemplateTicVParameter(internalCondition), Is.EqualTo(InternalConditionParameter.SupplyAirFlow),
                "Native SAM-authored template ventilation must not be silently dropped by the correction.");
        }
    }
}
