// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        /// <param name="paneLayers">The construction's layers; null when not read.</param>
        /// <param name="frames">The layers of each different frame construction of its elements' frame elements; null when not read.</param>
        /// <param name="zoneSurfaces">The number of zone surfaces of its elements, by zone name; null when not read.</param>
        public TasGlazingConstructionInfo(string name, IEnumerable<string> elements, double g, double u, double light, IEnumerable<TasMaterialLayer> paneLayers = null, IEnumerable<IEnumerable<TasMaterialLayer>> frames = null, IDictionary<string, int> zoneSurfaces = null)
        {
            Name = name;
            Elements = (elements ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
            G = g;
            U = u;
            Light = light;
            PaneLayers = paneLayers?.ToList().AsReadOnly();
            Frames = frames?.Select(x => (IReadOnlyList<TasMaterialLayer>)(x ?? Enumerable.Empty<TasMaterialLayer>()).ToList().AsReadOnly()).ToList().AsReadOnly();
            ZoneSurfaces = zoneSurfaces == null ? null : new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(zoneSurfaces, StringComparer.Ordinal));
        }

        public string Name { get; }

        /// <summary>The names of the building elements using the construction.</summary>
        public IReadOnlyList<string> Elements { get; }

        public double G { get; }

        public double U { get; }

        public double Light { get; }

        /// <summary>
        /// The pane construction's layers as the TBD holds them (material, thickness, properties); null when not read. What
        /// "Apply best design" compares the open model's aperture construction with.
        /// </summary>
        public IReadOnlyList<TasMaterialLayer> PaneLayers { get; }

        /// <summary>
        /// The frames of the window: the layers of each different construction of the frame building elements that pair
        /// with its pane elements (same <c>Windows: &lt;base&gt;</c>, <c>-frame</c>); empty when there is none; null when
        /// not read.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<TasMaterialLayer>> Frames { get; }

        /// <summary>
        /// Where the construction is: the number of zone surfaces of its building elements in each zone (by zone name, one
        /// per aperture and adjacent zone); null when not read.
        /// </summary>
        public IReadOnlyDictionary<string, int> ZoneSurfaces { get; }
    }
}
