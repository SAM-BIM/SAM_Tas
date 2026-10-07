// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Globalization;
using System.Threading;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// Owner decisions D1 and D2: the values the native adapter uses are the ones Java GenOpt 3.1.1 reads from the text
    /// the existing writer produces. Every expectation is a value observed through the real genopt.jar in the PR3
    /// Java-only oracle (GoldenSection bounds are not rounded, so the bits are visible), or PR1's confirmed spec.
    /// </summary>
    [TestFixture]
    public class GenOptNumberTests
    {
        private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        [TestCase(0.9, "0.9")]
        [TestCase(-0.0, "-0")]
        [TestCase(1e-5, "1E-05")]
        [TestCase(1.5e20, "1.5E+20")]
        [TestCase(1e15, "1000000000000000")]                     // .NET 8 stays plain here (PR3 oracle Command.txt)
        [TestCase(1e16, "10000000000000000")]
        [TestCase(1e25, "1E+25")]
        [TestCase(123456789012.5, "123456789012.5")]
        [TestCase(2147483648.0, "2147483648")]
        public void WriterText_IsTheJavaRouteText(double value, string expected)
        {
            Assert.That(GenOptNumber.WriterText(value), Is.EqualTo(expected));
        }

        /// <summary>Values observed through Java GenOpt 3.1.1 (PR3 oracle, GoldenSection bounds read bit for bit).</summary>
        [TestCase(0.30000000000000004, 0.30000000000000004)]   // 17 digits: inexact accumulation, still this value
        [TestCase(1.5e20, 1.5e20)]                             // "1.5E+20": plus-sign exponent accepted
        [TestCase(1.5e-30, 1.5000000000000001e-30)]            // "1.5E-30": num *= pow(10, -30) rounds twice
        [TestCase(1e15, 1e15)]
        [TestCase(1e25, 1e25)]                                 // "1E+25"
        [TestCase(123456789012.5, 123456789012.5)]
        [TestCase(2147483648.0, 2147483648.0)]                 // outside int: no (int) round trip
        [TestCase(0.1234567890123456, 0.1234567890123456)]
        [TestCase(987654.3210987654, 987654.3210987654)]
        public void ReadParameterValue_MatchesJavaGenOpt(double value, double javaRead)
        {
            Assert.That(SameBits(GenOptNumber.ReadParameterValue(value, "x"), javaRead), Is.True, GenOptNumber.ReadParameterValue(value, "x").ToString("R", CultureInfo.InvariantCulture));
        }

        [Test]
        public void ReadParameterValue_NegativeZero_BecomesPositiveZero()
        {
            double read = GenOptNumber.ReadParameterValue(-0.0, "x");

            Assert.That(SameBits(read, 0.0), Is.True);
            Assert.That(SameBits(GenOptNumber.Read("-0"), 0.0), Is.True);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void ReadParameterValue_NonFinite_IsRefused(double value)
        {
            Assert.Throws<GenOptCompatibilityException>(() => GenOptNumber.ReadParameterValue(value, "x"));
        }

        /// <summary>
        /// D2: Java GenOpt 3.1.1 rejects exponent notation for algorithm keywords. The PR3 oracle, with the existing
        /// writer and AbsDiffFunction = 1e-5 written as "1E-05", gave "Expected ';', got 'E-05'"; PR1 saw the same for
        /// "1E-300". The adapter refuses any such value.
        /// </summary>
        [TestCase(1e-5)]
        [TestCase(1e-31)]
        [TestCase(5e-7)]
        [TestCase(1e25)]
        [TestCase(double.NaN)]
        public void ReadAlgorithmValue_ExponentNotation_IsRefusedLikeJava(double value)
        {
            GenOptCompatibilityException exception = Assert.Throws<GenOptCompatibilityException>(() => GenOptNumber.ReadAlgorithmValue(value, "AbsDiffFunction"));

            Assert.That(exception.Message, Does.Contain("AbsDiffFunction"));
        }

        /// <summary>Plain decimals that the PR3 oracle and PR1 traces accepted.</summary>
        [TestCase(1.0)]
        [TestCase(0.1)]
        [TestCase(0.0001)]
        public void ReadAlgorithmValue_PlainDecimal_IsAccepted(double value)
        {
            Assert.That(GenOptNumber.ReadAlgorithmValue(value, "AbsDiffFunction"), Is.EqualTo(value));
        }

        [Test]
        public void Emulation_DoesNotDependOnTheCurrentCulture()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");

                Assert.That(GenOptNumber.WriterText(0.9), Is.EqualTo("0.9"));
                Assert.That(GenOptNumber.ReadParameterValue(0.05, "x"), Is.EqualTo(0.05));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestCase("")]
        [TestCase("-")]
        [TestCase("abc")]
        [TestCase("1,5")]
        [TestCase("1E")]
        public void Read_UnreadableText_IsRefused(string text)
        {
            Assert.Throws<GenOptCompatibilityException>(() => GenOptNumber.Read(text));
        }
    }
}
