// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>
    /// What a native reader found for one field. The five kinds are deliberately distinct (S07 section 6.1,
    /// reader precondition): an absent object, a field the reader could not read, a field read as NaN, a field
    /// read as exactly zero and a field read as a finite non-zero number never stand in for each other.
    /// </summary>
    public enum ReaderKind
    {
        /// <summary>The native object or field does not exist (proven by the reader, not assumed).</summary>
        Absent,

        /// <summary>The reader could not read the field (licence, executable, COM failure, field unavailable). Never a NaN.</summary>
        Unreadable,

        /// <summary>The field was read and holds NaN, or a non-finite number that cannot be compared.</summary>
        NaN,

        /// <summary>The field was read and is exactly zero.</summary>
        Zero,

        /// <summary>The field was read and is a finite non-zero number.</summary>
        Finite
    }

    /// <summary>
    /// One native read-back value together with how it was obtained. Immutable. Use the factory methods.
    /// </summary>
    public sealed class ReaderResult
    {
        private ReaderResult(ReaderKind kind, double value, string detail)
        {
            Kind = kind;
            Value = value;
            Detail = detail;
        }

        public ReaderKind Kind { get; }

        /// <summary>The number read. Only meaningful when <see cref="HasNumber"/>; NaN otherwise.</summary>
        public double Value { get; }

        /// <summary>Why the field is absent or unreadable, or a note on a non-finite value. May be null.</summary>
        public string Detail { get; }

        /// <summary>True for <see cref="ReaderKind.Zero"/> and <see cref="ReaderKind.Finite"/>.</summary>
        public bool HasNumber => Kind == ReaderKind.Zero || Kind == ReaderKind.Finite;

        public static ReaderResult Absent(string detail = null)
        {
            return new ReaderResult(ReaderKind.Absent, double.NaN, detail);
        }

        public static ReaderResult Unreadable(string reason)
        {
            return new ReaderResult(ReaderKind.Unreadable, double.NaN, reason);
        }

        public static ReaderResult NotANumber(string detail = null)
        {
            return new ReaderResult(ReaderKind.NaN, double.NaN, detail);
        }

        /// <summary>
        /// Classifies a number a reader has read: NaN and infinities are <see cref="ReaderKind.NaN"/> (not
        /// comparable), exactly 0 (also -0) is <see cref="ReaderKind.Zero"/>, anything else finite.
        /// </summary>
        public static ReaderResult FromValue(double value)
        {
            if (double.IsNaN(value))
            {
                return NotANumber();
            }

            if (double.IsInfinity(value))
            {
                return NotANumber("infinite");
            }

            if (value == 0)
            {
                return new ReaderResult(ReaderKind.Zero, 0, null);
            }

            return new ReaderResult(ReaderKind.Finite, value, null);
        }

        public override string ToString()
        {
            return HasNumber ? Kind + "(" + Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")" : Kind + (string.IsNullOrEmpty(Detail) ? string.Empty : " [" + Detail + "]");
        }
    }
}
