// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Globalization;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// One option of a glazing choice: option 1 is the glazing the model uses now; the others are pool systems, in g
    /// order. <see cref="Text"/> is what the definition lists in <c>target.options</c>.
    /// </summary>
    public sealed class TasGlazingOption
    {
        public TasGlazingOption(int number, string text, string paneConstruction, double g, double u, double light, string source, TasGlazingSystem system)
        {
            Number = number;
            Text = text;
            PaneConstruction = paneConstruction;
            G = g;
            U = u;
            Light = light;
            Source = source;
            System = system;
        }

        /// <summary>The option number, 1..n.</summary>
        public int Number { get; }

        /// <summary>
        /// The option as the definition names it: the current TBD construction name for option 1; otherwise the system's
        /// name, with its 6-character short id appended when two pool systems share the name.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// The TBD pane construction the script assigns: the current construction for option 1; for a pool system the
        /// name SAM_Tas writes it under before the run ("Windows: &lt;name&gt; &lt;short id&gt; -pane"). Null when the
        /// system cannot be written (no <see cref="TasGlazingSystem.ApertureConstruction"/>).
        /// </summary>
        public string PaneConstruction { get; }

        public double G { get; }

        /// <summary>The pane U-value (W/m²K).</summary>
        public double U { get; }

        public double Light { get; }

        /// <summary>"Model (current)" for option 1, otherwise the pool source.</summary>
        public string Source { get; }

        /// <summary>The pool system; null for option 1 (the glazing already in the TBD).</summary>
        public TasGlazingSystem System { get; }

        public bool IsCurrent => System == null;

        /// <summary>What the window shows for the option, for example "2 Triple low-e: g 0.369, Ug 1.00, light 0.728 (My glazing systems)".</summary>
        public string Summary => string.Format(CultureInfo.InvariantCulture, "{0} {1}: g {2:0.000}, Ug {3:0.00}, light {4:0.000} ({5})", Number, Text, G, U, Light, Source);
    }
}
