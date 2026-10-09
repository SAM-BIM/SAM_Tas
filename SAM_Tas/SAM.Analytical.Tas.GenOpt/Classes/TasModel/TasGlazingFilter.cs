// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// Which pool systems become options of a glazing choice. The defaults are the PR7a-2 owner-approved ones: same
    /// aperture type and transparency, Ug at most 0.3 W/m²K above the current glazing, light transmittance at least 0.1
    /// below it, duplicates once, at most 8 options with the current glazing as option 1. The g range is open unless set.
    /// </summary>
    public sealed class TasGlazingFilter
    {
        /// <summary>Lowest g accepted (inclusive); null for no limit.</summary>
        public double? MinimumG { get; set; }

        /// <summary>Highest g accepted (inclusive); null for no limit.</summary>
        public double? MaximumG { get; set; }

        /// <summary>How much worse (higher) than the current glazing's U a system's Ug may be, in W/m²K.</summary>
        public double UgAllowance { get; set; } = 0.3;

        /// <summary>How much lower than the current glazing's light transmittance a system's may be.</summary>
        public double LightAllowance { get; set; } = 0.1;

        /// <summary>The most options, the current glazing included (each option is one simulation).</summary>
        public int MaximumOptions { get; set; } = TasModelKind.MaximumGlazingOptions;

        /// <summary>The aperture type of the systems accepted ("Window").</summary>
        public string ApertureType { get; set; } = "Window";
    }
}
