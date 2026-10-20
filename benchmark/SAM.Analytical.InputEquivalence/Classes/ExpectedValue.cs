// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>
    /// The SAM input state of S07 section 6 (S-ZERO, S-UNSPEC, S-INVALID, S-VALUE).
    /// </summary>
    public enum SamInputState
    {
        /// <summary>S-ZERO: present and equal to 0.</summary>
        Zero,

        /// <summary>S-UNSPEC: absent or NaN, with no SAM derivation. Absence is proven by this state.</summary>
        Unspecified,

        /// <summary>S-INVALID: out of range, or a dependent value is NaN. Never compared.</summary>
        Invalid,

        /// <summary>S-VALUE: valid and non-zero.</summary>
        Value
    }

    /// <summary>
    /// The SAM-side expectation for one quantity: the S07 input state and, for Zero and Value, the number.
    /// Expected values come from raw stored SAM parameters with the formulas of S07, never from a converter.
    /// </summary>
    public sealed class ExpectedValue
    {
        private ExpectedValue(SamInputState state, double value, string detail)
        {
            State = state;
            Value = value;
            Detail = detail;
        }

        public SamInputState State { get; }

        /// <summary>The expected number for <see cref="SamInputState.Zero"/> (0) and <see cref="SamInputState.Value"/>; NaN otherwise.</summary>
        public double Value { get; }

        /// <summary>The reason for <see cref="SamInputState.Invalid"/>, or a note. May be null.</summary>
        public string Detail { get; }

        public static ExpectedValue Zero()
        {
            return new ExpectedValue(SamInputState.Zero, 0, null);
        }

        public static ExpectedValue Unspecified(string detail = null)
        {
            return new ExpectedValue(SamInputState.Unspecified, double.NaN, detail);
        }

        public static ExpectedValue Invalid(string reason)
        {
            return new ExpectedValue(SamInputState.Invalid, double.NaN, reason);
        }

        /// <summary>A valid finite number. 0 is <see cref="SamInputState.Zero"/>; NaN is <see cref="SamInputState.Unspecified"/>; infinities are <see cref="SamInputState.Invalid"/>.</summary>
        public static ExpectedValue FromValue(double value)
        {
            if (double.IsNaN(value))
            {
                return Unspecified();
            }

            if (double.IsInfinity(value))
            {
                return Invalid("infinite");
            }

            return value == 0 ? Zero() : new ExpectedValue(SamInputState.Value, value, null);
        }

        public override string ToString()
        {
            return State == SamInputState.Value || State == SamInputState.Zero
                ? State + "(" + Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")"
                : State + (string.IsNullOrEmpty(Detail) ? string.Empty : " [" + Detail + "]");
        }
    }
}
