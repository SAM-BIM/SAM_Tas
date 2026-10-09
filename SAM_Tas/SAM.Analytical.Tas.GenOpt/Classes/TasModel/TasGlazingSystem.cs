// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// One glazing system of the pool a glazing choice draws its options from (the SAM UI Glazing window's pool: the
    /// model, the default library, "My glazing systems", a loaded file), with the values the window shows. The values
    /// come from SAM_Tas' <c>ThermalTransmittanceCalculator.CalculateGlazing</c> (<see cref="Query.TasGlazingSystems"/>)
    /// or from the caller, who may already have them.
    /// </summary>
    public sealed class TasGlazingSystem
    {
        /// <param name="guid">The system's identity (names repeat).</param>
        /// <param name="name">The system's name.</param>
        /// <param name="source">Where it comes from, in words: "Model", "Default library", "My glazing systems", a file name.</param>
        /// <param name="apertureType">"Window" or "Door".</param>
        /// <param name="transparent">True when the pane is transparent.</param>
        /// <param name="g">Total solar energy transmittance of the pane.</param>
        /// <param name="ug">Pane thermal transmittance (W/m²K).</param>
        /// <param name="light">Light transmittance of the pane.</param>
        /// <param name="uf">Frame thermal transmittance (W/m²K); NaN when the system has no frame.</param>
        public TasGlazingSystem(Guid guid, string name, string source, string apertureType, bool transparent, double g, double ug, double light, double uf = double.NaN)
        {
            Guid = guid;
            Name = name;
            Source = source;
            ApertureType = apertureType;
            Transparent = transparent;
            G = g;
            Ug = ug;
            Light = light;
            Uf = uf;
        }

        public Guid Guid { get; }

        public string Name { get; }

        public string Source { get; }

        public string ApertureType { get; }

        public bool Transparent { get; }

        public double G { get; }

        public double Ug { get; }

        public double Light { get; }

        public double Uf { get; }

        /// <summary>The last 6 characters of the Guid, as the SAM UI Glazing window shows it (<c>GlazingCandidate.ShortId</c>).</summary>
        public string ShortId => Guid.ToString().Substring(30);

        /// <summary>
        /// The system itself, written into the run's TBD before the first simulation; null when only its values are
        /// known (then it can be listed but not run).
        /// </summary>
        public ApertureConstruction ApertureConstruction { get; set; }

        /// <summary>The materials the system's layers name (from its source).</summary>
        public global::SAM.Core.MaterialLibrary MaterialLibrary { get; set; }
    }
}
