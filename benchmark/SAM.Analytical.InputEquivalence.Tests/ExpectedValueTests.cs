// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence.Tests
{
    [TestFixture]
    public class ExpectedGeometryTests
    {
        [Test]
        public void Floor_Area_Is_The_Sum_Of_The_Authored_Floor_Polygons()
        {
            ExpectedValue one = Query.ExpectedFloorArea(new[] { TestData.Rectangle(5, 4) });
            Assert.That(one.State, Is.EqualTo(SamInputState.Value));
            Assert.That(one.Value, Is.EqualTo(20).Within(1e-9));

            ExpectedValue two = Query.ExpectedFloorArea(new[] { TestData.Rectangle(5, 4), TestData.Rectangle(2, 3, 10, 0, 0) });
            Assert.That(two.Value, Is.EqualTo(26).Within(1e-9));
        }

        [Test]
        public void Floor_Area_Of_Nothing_Or_Of_A_Degenerate_Polygon_Is_Invalid_Not_Zero()
        {
            Assert.That(Query.ExpectedFloorArea(null).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedFloorArea(new Polygon3D[0]).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedFloorArea(new Polygon3D[] { null }).State, Is.EqualTo(SamInputState.Invalid));
        }

        [Test]
        public void A_Tilted_Polygon_Is_Measured_On_Its_Own_Plane()
        {
            // 3 x 4 rectangle tilted about the x axis: its true area is 12, not the plan projection.
            Polygon3D tilted = new Polygon3D(new[] { new Point3D(0, 0, 0), new Point3D(3, 0, 0), new Point3D(3, 4 * 0.6, 4 * 0.8), new Point3D(0, 4 * 0.6, 4 * 0.8) });
            Assert.That(Query.GrossArea(tilted), Is.EqualTo(12).Within(1e-9));
        }

        [Test]
        public void Shell_Volume_Of_A_Closed_Box_Is_Its_Volume_Wherever_It_Is()
        {
            Assert.That(Query.ExpectedShellVolume(TestData.Box(5, 4, 3)).Value, Is.EqualTo(60).Within(1e-6));
            Assert.That(Query.ExpectedShellVolume(TestData.Box(10, 5, 3, 7, -2, 12)).Value, Is.EqualTo(150).Within(1e-6));
        }

        [Test]
        public void Shell_Volume_Is_Invalid_For_An_Open_Or_Missing_Shell()
        {
            List<Polygon3D> open = TestData.Box(5, 4, 3);
            open.RemoveAt(1);
            Assert.That(Query.ExpectedShellVolume(open).State, Is.EqualTo(SamInputState.Invalid), "a missing roof leaves the shell open");
            Assert.That(Query.ExpectedShellVolume(null).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedShellVolume(new Polygon3D[0]).State, Is.EqualTo(SamInputState.Invalid));

            List<Polygon3D> withNull = TestData.Box(5, 4, 3);
            withNull[2] = null;
            Assert.That(Query.ExpectedShellVolume(withNull).State, Is.EqualTo(SamInputState.Invalid));
        }

        [Test]
        public void Shell_Volume_Agrees_With_An_Independent_Divergence_Computation()
        {
            // Cross-check against signed-volume of triangles computed here from the same corner points.
            double analytic = 4.5 * 3.25 * 2.75;
            Assert.That(Query.ExpectedShellVolume(TestData.Box(4.5, 3.25, 2.75)).Value, Is.EqualTo(analytic).Within(1e-6));
        }

        [Test]
        public void Net_Area_Subtracts_The_Holes_And_Gross_Area_Does_Not()
        {
            Polygon3D wall = TestData.Rectangle(5, 3);
            Polygon3D window = TestData.Rectangle(2, 1.5, 1, 0.5);
            Assert.That(Query.GrossArea(wall), Is.EqualTo(15).Within(1e-9));
            Assert.That(Query.ExpectedNetArea(wall, new[] { window }).Value, Is.EqualTo(12).Within(1e-9));
            Assert.That(Query.ExpectedNetArea(wall, null).Value, Is.EqualTo(15).Within(1e-9));
        }

        [Test]
        public void Net_Area_Is_Invalid_When_The_Holes_Remove_The_Face_Or_A_Polygon_Is_Missing()
        {
            Polygon3D wall = TestData.Rectangle(2, 2);
            Assert.That(Query.ExpectedNetArea(wall, new[] { TestData.Rectangle(2, 2) }).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedNetArea(null, null).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedNetArea(wall, new Polygon3D[] { null }).State, Is.EqualTo(SamInputState.Invalid));
        }

        [TestCase(1.0, 3.0, 25.0)]
        [TestCase(2.0, 2.0, 50.0)]
        [TestCase(0.0, 5.0, 0.0)]
        [TestCase(double.NaN, 5.0, 0.0)]
        [TestCase(1.0, 0.0, 100.0)]
        [TestCase(1.0, double.NaN, 100.0)]
        [TestCase(0.0, 0.0, 0.0)]
        public void Frame_Percentage_Follows_The_Writer_Rule_Including_Its_Edge_Order(double frame, double pane, double expected)
        {
            ExpectedValue result = Query.ExpectedFramePercentage(frame, pane);
            Assert.That(result.State, Is.AnyOf(SamInputState.Value, SamInputState.Zero));
            Assert.That(result.Value, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void Frame_Percentage_Is_Not_Rounded_On_The_Contract_Route_And_Rejects_Negative_Areas()
        {
            Assert.That(Query.ExpectedFramePercentage(1, 2).Value, Is.EqualTo(100.0 / 3).Within(1e-12), "no one-decimal rounding without SharedWindowTypes");
            Assert.That(Query.ExpectedFramePercentage(-1, 2).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedFramePercentage(1, double.PositiveInfinity).State, Is.EqualTo(SamInputState.Invalid));
        }

        [Test]
        public void Frame_Width_Takes_The_Frame_Thickness_Before_The_Default_Frame_Width()
        {
            Assert.That(Query.ExpectedFrameWidth(0.07, 0.05).Value, Is.EqualTo(0.07), "the layer thickness sum overrides");
            Assert.That(Query.ExpectedFrameWidth(double.NaN, 0.05).Value, Is.EqualTo(0.05), "the default only when the thickness is NaN");
            Assert.That(Query.ExpectedFrameWidth(0, 0.05).State, Is.EqualTo(SamInputState.Zero), "no frame layers: thickness 0, the default does not apply");
            Assert.That(Query.ExpectedFrameWidth(double.NaN, double.NaN).State, Is.EqualTo(SamInputState.Unspecified));
            Assert.That(Query.ExpectedFrameWidth(-0.1, 0.05).State, Is.EqualTo(SamInputState.Invalid));
        }
    }

    [TestFixture]
    public class ExpectedLoadsTests
    {
        [Test]
        public void Gain_Density_Is_Power_Over_The_Stored_Area()
        {
            Assert.That(Query.ExpectedGainDensity(500, 50).Value, Is.EqualTo(10).Within(1e-12));
            Assert.That(Query.ExpectedGainDensity(0, 50).State, Is.EqualTo(SamInputState.Zero));
            Assert.That(Query.ExpectedGainDensity(double.NaN, 50).State, Is.EqualTo(SamInputState.Unspecified));
        }

        [TestCase(double.NaN)]
        [TestCase(0.0)]
        [TestCase(-5.0)]
        [TestCase(double.PositiveInfinity)]
        public void A_Stated_Gain_With_A_Missing_Or_Non_Positive_Stored_Area_Is_Invalid(double area)
        {
            Assert.That(Query.ExpectedGainDensity(500, area).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedPeopleDensity(double.NaN, 13.3, area).State, Is.EqualTo(SamInputState.Invalid));
        }

        [Test]
        public void A_Negative_Or_Infinite_Gain_Is_Invalid()
        {
            Assert.That(Query.ExpectedGainDensity(-1, 50).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedGainDensity(double.PositiveInfinity, 50).State, Is.EqualTo(SamInputState.Invalid));
        }

        [Test]
        public void People_Density_Uses_Occupancy_Else_Area_Over_Area_Per_Person()
        {
            Assert.That(Query.ExpectedPeopleDensity(4, double.NaN, 50).Value, Is.EqualTo(0.08).Within(1e-12));
            Assert.That(Query.ExpectedPeopleDensity(double.NaN, 12.5, 50).Value, Is.EqualTo(1 / 12.5).Within(1e-12), "(Area / AreaPerPerson) / Area");
            Assert.That(Query.ExpectedPeopleDensity(4, 99, 50).Value, Is.EqualTo(0.08).Within(1e-12), "Occupancy wins");
            Assert.That(Query.ExpectedPeopleDensity(double.NaN, double.NaN, 50).State, Is.EqualTo(SamInputState.Unspecified));
            Assert.That(Query.ExpectedPeopleDensity(double.NaN, 0, 50).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedPeopleDensity(0, double.NaN, 50).State, Is.EqualTo(SamInputState.Zero));
        }

        [Test]
        public void Infiltration_Flow_Is_Ach_Times_Volume_Over_3600()
        {
            Assert.That(Query.ExpectedInfiltrationFlow(0.5, 150).Value, Is.EqualTo(0.5 * 150 / 3600).Within(1e-15));
            Assert.That(Query.ExpectedInfiltrationFlow(0, 150).State, Is.EqualTo(SamInputState.Zero));
            Assert.That(Query.ExpectedInfiltrationFlow(double.NaN, 150).State, Is.EqualTo(SamInputState.Unspecified));
            Assert.That(Query.ExpectedInfiltrationFlow(0.5, double.NaN).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedInfiltrationFlow(0.5, 0).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedInfiltrationFlow(-1, 150).State, Is.EqualTo(SamInputState.Invalid));
        }

        // ---------------------------------------------------------------- ventilation D-F2

        [Test]
        public void Ventilation_State_Follows_D_F2()
        {
            VentilationRequirement requirement = new VentilationRequirement { FlowPerPerson = 0.012 };
            VentilationRequirement none = new VentilationRequirement();
            VentilationRequirement zero = new VentilationRequirement { Flow = 0 };

            Assert.That(Query.ClassifyVentilation(null, false), Is.EqualTo(VentilationState.V0));
            Assert.That(Query.ClassifyVentilation(none, false), Is.EqualTo(VentilationState.V0));
            Assert.That(Query.ClassifyVentilation(zero, false), Is.EqualTo(VentilationState.V0), "a stated zero is no requirement");
            Assert.That(Query.ClassifyVentilation(requirement, false), Is.EqualTo(VentilationState.V1));
            Assert.That(Query.ClassifyVentilation(requirement, true), Is.EqualTo(VentilationState.V2));
            Assert.That(Query.ClassifyVentilation(none, true), Is.EqualTo(VentilationState.Indeterminate), "a profile without a requirement is not defined by D-F2");
        }

        [Test]
        public void The_Required_Supply_Flow_Sums_The_Four_Bases()
        {
            VentilationRequirement all = new VentilationRequirement { Flow = 0.01, FlowPerArea = 0.0002, FlowPerPerson = 0.012, AirChangesPerHour = 0.36 };
            ExpectedValue result = Query.ExpectedRequiredSupplyFlow(all, 50, 4, 150);
            double expected = 0.01 + 0.0002 * 50 + 0.012 * 4 + 0.36 * 150 / 3600;
            Assert.That(result.Value, Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void The_Required_Supply_Flow_Rejects_A_Base_That_Needs_A_Missing_Dependency()
        {
            Assert.That(Query.ExpectedRequiredSupplyFlow(new VentilationRequirement { FlowPerArea = 0.0002 }, double.NaN, 4, 150).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedRequiredSupplyFlow(new VentilationRequirement { FlowPerPerson = 0.012 }, 50, double.NaN, 150).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedRequiredSupplyFlow(new VentilationRequirement { AirChangesPerHour = 0.5 }, 50, 4, 0).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedRequiredSupplyFlow(new VentilationRequirement { Flow = -1 }, 50, 4, 150).State, Is.EqualTo(SamInputState.Invalid));
            Assert.That(Query.ExpectedRequiredSupplyFlow(new VentilationRequirement(), 50, 4, 150).State, Is.EqualTo(SamInputState.Unspecified));
            Assert.That(Query.ExpectedRequiredSupplyFlow(null, 50, 4, 150).State, Is.EqualTo(SamInputState.Unspecified));
            Assert.That(Query.ExpectedRequiredSupplyFlow(new VentilationRequirement { Flow = 0 }, 50, 4, 150).State, Is.EqualTo(SamInputState.Zero));
        }

        [Test]
        public void Requirement_And_Delivery_Are_Separate_V1_Delivers_Nothing_V2_Delivers_The_Requirement()
        {
            ExpectedValue required = ExpectedValue.FromValue(0.048);

            Assert.That(Query.ExpectedDeliveredOutdoorAir(VentilationState.V0, required).State, Is.EqualTo(SamInputState.Unspecified));
            Assert.That(Query.ExpectedDeliveredOutdoorAir(VentilationState.V1, required).State, Is.EqualTo(SamInputState.Unspecified), "V1: the requirement alone activates nothing");
            Assert.That(Query.ExpectedDeliveredOutdoorAir(VentilationState.V2, required).Value, Is.EqualTo(0.048));
            Assert.That(Query.ExpectedDeliveredOutdoorAir(VentilationState.Indeterminate, required).State, Is.EqualTo(SamInputState.Invalid));
        }

        [Test]
        public void In_V1_An_Absent_Ticv_Is_Not_Applicable_And_A_Present_One_Is_An_Export_Defect()
        {
            ExpectedValue delivered = Query.ExpectedDeliveredOutdoorAir(VentilationState.V1, ExpectedValue.FromValue(0.048));

            QuantityResult absent = Query.Evaluate(TestData.Input("8b", delivered, ReaderResult.Absent()));
            Assert.That(absent.Outcome, Is.EqualTo(EquivalenceOutcome.NotApplicable));
            Assert.That(absent.IsPass, Is.True);

            QuantityResult present = Query.Evaluate(TestData.Input("8b", delivered, ReaderResult.FromValue(0.048)));
            Assert.That(present.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect), "a delivery nobody asked for");
            Assert.That(present.Status, Is.EqualTo(GateStatus.Fail));

            QuantityResult zero = Query.Evaluate(TestData.Input("8b", delivered, ReaderResult.FromValue(0)));
            Assert.That(zero.IsPass, Is.False, "a present zero ticv is the undecided G1 case, not an absence");
        }

        [Test]
        public void Native_Flow_Propagates_Reader_States_And_Never_Turns_Them_Into_Numbers()
        {
            ReaderResult ach = ReaderResult.FromValue(0.5);
            ReaderResult volume = ReaderResult.FromValue(150);

            Assert.That(Query.Multiply(ach, volume, 1.0 / 3600).Value, Is.EqualTo(0.5 * 150 / 3600).Within(1e-15));
            Assert.That(Query.Multiply(ach, ReaderResult.Unreadable("u")).Kind, Is.EqualTo(ReaderKind.Unreadable));
            Assert.That(Query.Multiply(ReaderResult.Absent(), volume).Kind, Is.EqualTo(ReaderKind.Absent));
            Assert.That(Query.Multiply(ach, ReaderResult.NotANumber()).Kind, Is.EqualTo(ReaderKind.NaN));
            Assert.That(Query.Multiply(ReaderResult.Unreadable("u"), ReaderResult.NotANumber()).Kind, Is.EqualTo(ReaderKind.Unreadable), "unreadable outranks NaN");
            Assert.That(Query.Multiply(ReaderResult.FromValue(0), volume).Kind, Is.EqualTo(ReaderKind.Zero));
            Assert.That(Query.Multiply(null, volume).Kind, Is.EqualTo(ReaderKind.Unreadable));
        }
    }

    [TestFixture]
    public class ExpectedThermalTests
    {
        private static LayerResistance[] Wall()
        {
            return new[] { LayerResistance.Solid(0.1, 0.8), LayerResistance.Solid(0.1, 0.04), LayerResistance.Solid(0.013, 0.16) };
        }

        [Test]
        public void The_Film_Constants_Are_The_Owner_Approved_Ones()
        {
            Assert.That(Query.OpaqueRse, Is.EqualTo(0.04));
            Assert.That(Query.OpaqueRsiHorizontal, Is.EqualTo(0.13));
            Assert.That(Query.OpaqueRsiUpward, Is.EqualTo(0.10));
            Assert.That(Query.OpaqueRsiDownward, Is.EqualTo(0.17));
            Assert.That(Query.GlazingHi, Is.EqualTo(7.7));
            Assert.That(Query.GlazingHe, Is.EqualTo(25));
            Assert.That(Query.GlazingGHe, Is.EqualTo(23));
            Assert.That(Query.GlazingGHi, Is.EqualTo(8));
        }

        [Test]
        public void An_External_Wall_U_Uses_Rsi_Horizontal_And_Rse()
        {
            double r = 0.1 / 0.8 + 0.1 / 0.04 + 0.013 / 0.16;
            Assert.That(Query.ExpectedOpaqueU(Wall(), HeatFlowDirection.Horizontal, ElementKind.External).Value, Is.EqualTo(1 / (0.13 + r + 0.04)).Within(1e-12));
        }

        [Test]
        public void The_Internal_Film_Depends_On_The_Heat_Flow_Direction()
        {
            double r = 0.1 / 0.8 + 0.1 / 0.04 + 0.013 / 0.16;
            Assert.That(Query.ExpectedOpaqueU(Wall(), HeatFlowDirection.Upward, ElementKind.External).Value, Is.EqualTo(1 / (0.10 + r + 0.04)).Within(1e-12));
            Assert.That(Query.ExpectedOpaqueU(Wall(), HeatFlowDirection.Downward, ElementKind.External).Value, Is.EqualTo(1 / (0.17 + r + 0.04)).Within(1e-12));
        }

        [Test]
        public void Internal_Elements_Have_Rsi_On_Both_Sides_And_Ground_Elements_Have_No_Rse()
        {
            double r = 0.1 / 0.8 + 0.1 / 0.04 + 0.013 / 0.16;
            Assert.That(Query.ExpectedOpaqueU(Wall(), HeatFlowDirection.Horizontal, ElementKind.Internal).Value, Is.EqualTo(1 / (0.13 + r + 0.13)).Within(1e-12));
            Assert.That(Query.ExpectedOpaqueU(Wall(), HeatFlowDirection.Downward, ElementKind.GroundOrAdiabatic).Value, Is.EqualTo(1 / (0.17 + r)).Within(1e-12));
        }

        [Test]
        public void Glazing_U_Uses_The_En_673_Films()
        {
            LayerResistance[] glazing = { LayerResistance.Solid(0.004, 1.0), LayerResistance.Fixed(0.15), LayerResistance.Solid(0.004, 1.0) };
            double expected = 1 / (1 / 7.7 + 0.004 + 0.15 + 0.004 + 1 / 25.0);
            Assert.That(Query.ExpectedGlazingU(glazing).Value, Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void An_Invalid_Layer_Makes_The_U_Value_Invalid_It_Is_Never_Skipped()
        {
            foreach (LayerResistance[] layers in new[]
            {
                new[] { LayerResistance.Solid(0.1, 0.8), LayerResistance.Solid(double.NaN, 0.04) },
                new[] { LayerResistance.Solid(0.1, 0.8), LayerResistance.Solid(0.1, 0) },
                new[] { LayerResistance.Solid(0.1, 0.8), LayerResistance.Solid(-0.1, 0.5) },
                new[] { LayerResistance.Solid(0.1, 0.8), LayerResistance.Fixed(double.NaN) },
                new[] { LayerResistance.Solid(0.1, 0.8), null },
                new LayerResistance[0]
            })
            {
                Assert.That(Query.ExpectedOpaqueU(layers, HeatFlowDirection.Horizontal, ElementKind.External).State, Is.EqualTo(SamInputState.Invalid));
                Assert.That(Query.ExpectedGlazingU(layers).State, Is.EqualTo(SamInputState.Invalid));
            }

            Assert.That(Query.ExpectedOpaqueU(null, HeatFlowDirection.Horizontal, ElementKind.External).State, Is.EqualTo(SamInputState.Invalid));
        }
    }

    [TestFixture]
    public class DesignDayTests
    {
        [TestCase(2026, 1, 1, 1)]
        [TestCase(2026, 12, 31, 365)]
        [TestCase(2024, 12, 31, 366)]
        [TestCase(2024, 3, 1, 61)]
        [TestCase(2026, 3, 1, 60)]
        public void Year_Day_Is_Computed_In_The_Date_Own_Year(int year, int month, int day, int expected)
        {
            Assert.That(Query.ExpectedYearDay(year, month, day).Value, Is.EqualTo(expected));
        }

        [Test]
        public void A_Leap_Year_Date_Differs_From_The_Same_Date_In_The_2018_Reference_Year()
        {
            // This is the reader's reference-year hazard: yearDay 61 is 1 March in 2024 but 2 March in 2018.
            Assert.That(Query.ExpectedYearDay(2024, 3, 1).Value, Is.EqualTo(61));
            Assert.That(Query.ExpectedYearDay(2018, 3, 1).Value, Is.EqualTo(60));
        }

        [TestCase(2026, 2, 29)]
        [TestCase(2026, 13, 1)]
        [TestCase(2026, 4, 31)]
        [TestCase(0, 1, 1)]
        public void An_Impossible_Date_Is_Invalid(int year, int month, int day)
        {
            Assert.That(Query.ExpectedYearDay(year, month, day).State, Is.EqualTo(SamInputState.Invalid));
        }

        [Test]
        public void Year_Day_Is_Compared_As_An_Exact_Integer()
        {
            Assert.That(Query.Evaluate(TestData.Input("10.yearDay", Query.ExpectedYearDay(2026, 1, 21), ReaderResult.FromValue(21))).IsPass, Is.True);
            QuantityResult off = Query.Evaluate(TestData.Input("10.yearDay", Query.ExpectedYearDay(2026, 1, 21), ReaderResult.FromValue(22)));
            Assert.That(off.Outcome, Is.EqualTo(EquivalenceOutcome.OutOfTolerance));
        }
    }
}
