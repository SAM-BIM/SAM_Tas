// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Globalization;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// The numbers Java GenOpt 3.1.1 actually works with when it reads the command file the GenOpt-format writer
    /// (<see cref="CommandFile"/>) produces. The native adapter applies this before building the generic SAM.Math
    /// problem, so native runs start from the values Java GenOpt started from (bit-identical in the PR3 oracle).
    /// <para>
    /// The writer formats each value with <c>double.ToString()</c> (.NET shortest round-trip text, e.g. "0.9",
    /// "1E-05", "1.5E+20", "-0"). GenOpt reads it with <c>java.io.StreamTokenizer</c> arithmetic, not a correctly
    /// rounded parser (SAM documentation/GenOpt-3.1.1-Behaviour.md §1.1):
    /// </para>
    /// <list type="bullet">
    /// <item>the mantissa is accumulated as v = v·10 + digit and divided by 10^d, with 10^d built by repeated
    /// multiplication;</item>
    /// <item>an exponent is applied as num *= Math.pow(10, exp), so it rounds twice;</item>
    /// <item>an integer-valued result passes through (int), so -0 becomes 0.</item>
    /// </list>
    /// <para>
    /// The writer's own culture dependence (it uses the current culture) is not reproduced: Java GenOpt only worked
    /// with a '.' decimal separator, so invariant text is the text GenOpt can read.
    /// </para>
    /// </summary>
    public static class GenOptNumber
    {
        /// <summary>The text the existing command-file writer produces for a value (invariant culture).</summary>
        public static string WriterText(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The double GenOpt reads for a parameter keyword (Ini, Min, Max, Step) written by the existing writer.
        /// Throws for NaN and infinities, which GenOpt cannot read.
        /// </summary>
        public static double ReadParameterValue(double value, string description)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new GenOptCompatibilityException(string.Format(CultureInfo.InvariantCulture, "{0} must be finite; its GenOpt text would be '{1}', which GenOpt 3.1.1 cannot read.", description, WriterText(value)));
            }

            return Read(WriterText(value));
        }

        /// <summary>
        /// The double GenOpt reads for a numeric algorithm keyword (e.g. AbsDiffFunction) written by the existing
        /// writer. GenOpt 3.1.1 rejects exponent notation there ("Expected ';', got 'E-05'"), so such a value is
        /// refused rather than accepted (owner decision D2).
        /// </summary>
        public static double ReadAlgorithmValue(double value, string keyword)
        {
            string text = WriterText(value);
            if (double.IsNaN(value) || double.IsInfinity(value) || text.IndexOf('E') >= 0 || text.IndexOf('e') >= 0)
            {
                throw new GenOptCompatibilityException(string.Format(CultureInfo.InvariantCulture, "{0} = {1} cannot be used: its GenOpt text is '{1}', and GenOpt 3.1.1 rejects exponent notation (or a non-finite value) for algorithm keywords. Use a value that is written as a plain decimal.", keyword, text));
            }

            return Read(text);
        }

        /// <summary>GenOpt's (StreamTokenizer) reading of command-file number text.</summary>
        public static double Read(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new GenOptCompatibilityException("Empty number text.");
            }

            int i = 0;
            bool negative = false;
            if (text[i] == '-')
            {
                negative = true;
                i++;
            }

            double v = 0;
            int decimalExponent = 0;
            int seenDot = 0;
            int digits = 0;
            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '.' && seenDot == 0)
                {
                    seenDot = 1;
                }
                else if (c >= '0' && c <= '9')
                {
                    v = (v * 10) + (c - '0');
                    decimalExponent += seenDot;
                    digits++;
                }
                else
                {
                    break;
                }
            }

            if (digits == 0)
            {
                throw new GenOptCompatibilityException("'" + text + "' is not a number GenOpt 3.1.1 can read.");
            }

            if (decimalExponent != 0)
            {
                double denominator = 10;
                decimalExponent--;
                while (decimalExponent > 0)
                {
                    denominator *= 10;
                    decimalExponent--;
                }

                v /= denominator;
            }

            double number = negative ? -v : v;

            if (i < text.Length)
            {
                int exponent;
                if ((text[i] != 'E' && text[i] != 'e') || !int.TryParse(text.Substring(i + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
                {
                    throw new GenOptCompatibilityException("'" + text + "' is not a number GenOpt 3.1.1 can read.");
                }

                number *= System.Math.Pow(10, exponent);
            }

            // genopt.io.Token passes an integer-valued number through Integer.toString((int)num); -0.0 becomes 0.
            if (number >= int.MinValue && number <= int.MaxValue && (int)number == number)
            {
                return (int)number;
            }

            return number;
        }
    }
}
