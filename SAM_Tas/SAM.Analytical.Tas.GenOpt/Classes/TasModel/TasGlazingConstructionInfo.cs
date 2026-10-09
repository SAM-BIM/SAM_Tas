// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// A TBD transparent construction used by building elements (the glazing a choice replaces), found through the
    /// elements so the <c>GetConstruction(i)</c> null gaps do not hide it (read-only data, no COM).
    /// </summary>
    public sealed class TasGlazingConstructionInfo
    {
        /// <param name="name">The exact TBD construction name.</param>
        /// <param name="elements">The building elements using it.</param>
        /// <param name="g">Total solar energy transmittance, <c>GetGlazingValues()[5]</c>.</param>
        /// <param name="u">Thermal transmittance, <c>GetUValue()[6]</c> (W/m²K).</param>
        /// <param name="light">Light transmittance, <c>GetGlazingValues()[0]</c>.</param>
        public TasGlazingConstructionInfo(string name, IEnumerable<string> elements, double g, double u, double light)
        {
            Name = name;
            Elements = (elements ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
            G = g;
            U = u;
            Light = light;
        }

        public string Name { get; }

        /// <summary>The names of the building elements using the construction.</summary>
        public IReadOnlyList<string> Elements { get; }

        public double G { get; }

        public double U { get; }

        public double Light { get; }
    }
}
