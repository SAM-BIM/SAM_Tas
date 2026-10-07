// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using SAM.Math;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// The Gate T file contract (TASGENEXECUTE_PROTOCOL.md §2.3), without running any process: Variables.txt
    /// generation, Output.txt objective parsing and failure classification.
    /// </summary>
    [TestFixture]
    public class ProtocolFileTests
    {
        private static NumberParameter Parameter(string name, double min = -1, double max = 1, double step = 0.05) => new NumberParameter { Name = name, Initial = 0, Min = min, Max = max, Step = step };

        private static readonly IReadOnlyList<Objective> ResultOnly = new List<Objective> { new Objective("Result") };

        // ------------------------------------------------------------------ Variables.txt

        [Test]
        public void VariablesText_OrdinaryValues()
        {
            string text = TasGenExecuteObjectiveEvaluator.VariablesText(new[] { Parameter("Setpoint", 0, 30, 1), Parameter("x2") }, new[] { 10.278640450004207, 0.9 });

            Assert.That(text, Is.EqualTo("Setpoint,10.278640450004207,0,30,1,System.Double\nx2,0.9,-1,1,0.05,System.Double"));
        }

        [Test]
        public void VariablesText_SignedZero_LargeAndSmall()
        {
            string text = TasGenExecuteObjectiveEvaluator.VariablesText(new[] { Parameter("a"), Parameter("b"), Parameter("c"), Parameter("d") }, new[] { -0.0, 0.0, 1e25, 1.5000000000000001e-30 });

            Assert.That(text.Split('\n'), Is.EqualTo(new[]
            {
                "a,-0,-1,1,0.05,System.Double",
                "b,0,-1,1,0.05,System.Double",
                "c,1E+25,-1,1,0.05,System.Double",
                "d,1.5000000000000001E-30,-1,1,0.05,System.Double",
            }));
        }

        /// <summary>
        /// The value written is the kernel coordinate: here the double Java GenOpt reads for an initial value of
        /// 1.5e-30. Min/Max/Step are the Java writer's text for the parameter's own values, which is what the Java
        /// route's template contains.
        /// </summary>
        [Test]
        public void VariablesText_ValueFromTheJavaMapping()
        {
            NumberParameter parameter = new NumberParameter { Name = "t", Initial = 1.5e-30, Min = 1e-31, Max = 1e15, Step = 1e-31 };
            double coordinate = parameter.ToSAM_OptimisationParameter().Initial;

            string text = TasGenExecuteObjectiveEvaluator.VariablesText(new[] { parameter }, new[] { coordinate });

            Assert.That(text, Is.EqualTo("t,1.5000000000000001E-30,1E-31,1000000000000000,1E-31,System.Double"));
        }

        [Test]
        public void VariablesText_IsInvariantUnderACommaCulture()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");

                Assert.That(TasGenExecuteObjectiveEvaluator.VariablesText(new[] { Parameter("x") }, new[] { 0.5 }), Is.EqualTo("x,0.5,-1,1,0.05,System.Double"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        // ------------------------------------------------------------------ Output.txt parsing

        [Test]
        public void TryReadObjective_DatePrefixedFirstLine()
        {
            Assert.That(TasGenExecuteObjectiveEvaluator.TryReadObjective(new[] { "07/10/2026 10:00:00::Result::7392.29388427734" }, "Result::", out double value, out _), Is.True);
            Assert.That(value, Is.EqualTo(7392.29388427734));
        }

        [Test]
        public void TryReadObjective_LastMatchingLineWins()
        {
            string[] lines = { "d::Result::1", "Other::5", "d::Result::2.5" };

            Assert.That(TasGenExecuteObjectiveEvaluator.TryReadObjective(lines, "Result::", out double value, out _), Is.True);
            Assert.That(value, Is.EqualTo(2.5));
        }

        [TestCase("Result::-1.5E-07", -1.5e-07)]
        [TestCase("Result::1E+15", 1e15)]
        [TestCase("Result:: 42 ", 42.0)]
        [TestCase("Result::NaN", double.NaN)]
        [TestCase("Result::-Infinity", double.NegativeInfinity)]
        public void TryReadObjective_NumericContract(string line, double expected)
        {
            Assert.That(TasGenExecuteObjectiveEvaluator.TryReadObjective(new[] { line }, "Result::", out double value, out _), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [TestCase("Result::7392,29", "comma")]
        [TestCase("Result::abc", "not a number")]
        [TestCase("Result::", "empty")]
        [TestCase("Result::1.5 kWh", "not a number")]
        [TestCase("Result::0x10", "not a number")]
        public void TryReadObjective_Malformed_Fails(string line, string reason)
        {
            Assert.That(TasGenExecuteObjectiveEvaluator.TryReadObjective(new[] { line }, "Result::", out _, out string message), Is.False);
            Assert.That(message, Does.Contain(reason));
        }

        [Test]
        public void TryReadObjective_Missing_Fails()
        {
            Assert.That(TasGenExecuteObjectiveEvaluator.TryReadObjective(new[] { "Cooling::5" }, "Result::", out _, out string message), Is.False);
            Assert.That(message, Does.Contain("Result::"));
        }

        // ------------------------------------------------------------------ classification

        private static ObjectiveEvaluation Classify(int exitCode, string output, string error, IReadOnlyList<Objective> objectives = null)
        {
            using (TestFolder folder = new TestFolder())
            {
                if (output != null)
                {
                    System.IO.File.WriteAllText(folder.Combine("Output.txt"), output);
                }

                if (error != null)
                {
                    System.IO.File.WriteAllText(folder.Combine("Error.txt"), error);
                }

                return TasGenExecuteObjectiveEvaluator.Classify(folder.Path, exitCode, objectives ?? ResultOnly);
            }
        }

        [Test]
        public void Classify_MultipleOutputs_InObjectiveOrder_FirstIsTheObjective()
        {
            List<Objective> objectives = new List<Objective> { new Objective("Result"), new Objective("Cooling"), new Objective("Heating") };

            ObjectiveEvaluation result = Classify(0, "d::Result::3\r\nHeating::7\r\nCooling::5\r\n", null, objectives);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Outputs, Is.EqualTo(new[] { 3.0, 5.0, 7.0 }));
        }

        [Test]
        public void Classify_NonZeroExit_Fails()
        {
            ObjectiveEvaluation result = Classify(unchecked((int)0xE0434352), "d::Result::3", null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("E0434352"));
        }

        [Test]
        public void Classify_NonEmptyErrorTxt_Fails_EvenWithAResult()
        {
            ObjectiveEvaluation result = Classify(0, "d::Result::3", "d::Error: (Line 3) -> \"boom\"");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("boom"));
        }

        /// <summary>A compile error is written with lowercase "error" only (Gate T T5); any content is a failure.</summary>
        [Test]
        public void Classify_LowercaseCompileError_Fails()
        {
            ObjectiveEvaluation result = Classify(0, string.Empty, "(3,5): error CS1002: ; expected");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("CS1002"));
        }

        [Test]
        public void Classify_WhitespaceOnlyErrorTxt_IsNotAFailure()
        {
            Assert.That(Classify(0, "d::Result::3", "  \r\n").Succeeded, Is.True);
        }

        [Test]
        public void Classify_NoOutputTxt_Fails()
        {
            Assert.That(Classify(0, null, null).Succeeded, Is.False);
        }

        [Test]
        public void Classify_MissingSecondaryObjective_Fails()
        {
            ObjectiveEvaluation result = Classify(0, "d::Result::3", null, new List<Objective> { new Objective("Result"), new Objective("Cooling") });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Cooling"));
        }

        [Test]
        public void EvaluationDirectory_FirstAttemptAndRetry_AreDistinct()
        {
            string root = Path.Combine("C:", "run", "evaluations");

            Assert.That(Path.GetFileName(TasGenExecuteObjectiveEvaluator.EvaluationDirectory(root, 4, 1)), Is.EqualTo("0004"));
            Assert.That(Path.GetFileName(TasGenExecuteObjectiveEvaluator.EvaluationDirectory(root, 4, 2)), Is.EqualTo("0004-retry"));
            Assert.That(Path.GetFileName(TasGenExecuteObjectiveEvaluator.EvaluationDirectory(root, 12345, 1)), Is.EqualTo("12345"));
        }

        /// <summary>Direct invocation: the executable itself, the quoted project as the only argument, the evaluation folder as cwd.</summary>
        [Test]
        public void StartInfo_IsADirectInvocation()
        {
            System.Diagnostics.ProcessStartInfo startInfo = TasGenExecuteObjectiveEvaluator.StartInfo(@"C:\Tas\TasGenExecute.exe", @"C:\run\project\", @"C:\run\evaluations\0001");

            Assert.That(startInfo.FileName, Is.EqualTo(@"C:\Tas\TasGenExecute.exe"));
            Assert.That(startInfo.Arguments, Is.EqualTo("\"C:\\run\\project\""));
            Assert.That(startInfo.WorkingDirectory, Is.EqualTo(@"C:\run\evaluations\0001"));
            Assert.That(startInfo.UseShellExecute, Is.False);
            Assert.That(startInfo.FileName, Does.Not.Contain("cmd").IgnoreCase.And.Not.Contain("java").IgnoreCase);
        }
    }
}
