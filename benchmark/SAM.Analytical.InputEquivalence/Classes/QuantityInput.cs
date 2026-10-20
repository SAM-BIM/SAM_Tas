// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>
    /// Everything the S07 section 6 evaluation needs for one quantity on one object of one fixture. The defaults
    /// are fail-closed: identity is <see cref="IdentityStatus.NotEstablished"/>, alignment is
    /// <see cref="AlignmentStatus.Unverified"/>, no limitation is listed, no transformation is verified.
    /// </summary>
    public sealed class QuantityInput
    {
        public QuantityDefinition Definition { get; init; }

        /// <summary>The fixture this result belongs to; the gate and the owner acceptances are per fixture.</summary>
        public string FixtureId { get; init; }

        /// <summary>
        /// A human-readable key of the compared object (space, panel side, aperture, profile hour). Reporting only;
        /// it is never evidence of identity.
        /// </summary>
        public string ObjectKey { get; init; }

        public ExpectedValue Expected { get; init; }
        public ReaderResult Native { get; init; }

        /// <summary>Independent identity evidence; see <see cref="Query.AssessIdentity"/>. Default: not established.</summary>
        public IdentityStatus Identity { get; init; } = IdentityStatus.NotEstablished;

        /// <summary>Only consulted when <see cref="QuantityDefinition.HourlyAligned"/>.</summary>
        public AlignmentStatus Alignment { get; init; } = AlignmentStatus.Unverified;

        /// <summary>A limitation S07 lists for this quantity. It explains a missing or differing native value only; it never covers a NaN.</summary>
        public ContractListing Listing { get; init; } = ContractListing.None;

        /// <summary>The contract-listed default for an S-UNSPEC quantity (S07 section 6), or null when none is listed.</summary>
        public double? ListedDefault { get; init; }

        /// <summary>The contract-listed transformation (T-DD, T-FRAME, T-OMIT0 …) that relates the expected to the native value, or null.</summary>
        public string TransformationId { get; init; }

        /// <summary>True when the transformation was recomputed independently and its precondition holds. An unverified transformation is never a pass.</summary>
        public bool TransformationVerified { get; init; }

        public ModelInterpretation Interpretation { get; init; } = ModelInterpretation.NotRequired;
    }
}
