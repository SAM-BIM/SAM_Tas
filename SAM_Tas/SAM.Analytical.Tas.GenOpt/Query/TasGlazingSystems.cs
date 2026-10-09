// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// One source of the glazing pool, calculated as the SAM UI Glazing window calculates it: every window or door
        /// system of <paramref name="constructionManager"/> through SAM_Tas' <c>ThermalTransmittanceCalculator.CalculateGlazing</c>
        /// (Tas TCD; g and light rounded to 0.001, Ug unrounded; the g it reports is the g the TBD simulates, PR7a-2
        /// question 2). Each system keeps its aperture construction and the source's material library, so the run can
        /// write it into its TBD. Licensed Tas; call it on an STA thread, as the Glazing window does.
        /// </summary>
        /// <param name="constructionManager">The source's systems and the materials they name.</param>
        /// <param name="source">The source in words: "Model", "Default library", "My glazing systems", a file name.</param>
        public static List<TasGlazingSystem> TasGlazingSystems(ConstructionManager constructionManager, string source)
        {
            List<TasGlazingSystem> result = new List<TasGlazingSystem>();
            List<ApertureConstruction> apertureConstructions = (constructionManager?.ApertureConstructions ?? new List<ApertureConstruction>()).Where(x => x != null).ToList();
            if (apertureConstructions.Count == 0)
            {
                return result;
            }

            List<GlazingCalculationResult> glazingCalculationResults = new ThermalTransmittanceCalculator(constructionManager).CalculateGlazing(apertureConstructions.Select(x => x.Guid)) ?? new List<GlazingCalculationResult>();
            foreach (ApertureConstruction apertureConstruction in apertureConstructions)
            {
                GlazingCalculationResult glazingCalculationResult = glazingCalculationResults.Find(x => x != null && x.Reference == apertureConstruction.Guid.ToString());
                if (glazingCalculationResult == null)
                {
                    continue;
                }

                double uf = glazingCalculationResult is ApertureGlazingCalculationResult apertureGlazingCalculationResult ? apertureGlazingCalculationResult.FrameThermalTransmittance : double.NaN;
                result.Add(new TasGlazingSystem(
                    apertureConstruction.Guid,
                    apertureConstruction.Name,
                    source,
                    apertureConstruction.ApertureType.ToString(),
                    apertureConstruction.Transparent(constructionManager.MaterialLibrary),
                    glazingCalculationResult.TotalSolarEnergyTransmittance,
                    glazingCalculationResult.ThermalTransmittance,
                    glazingCalculationResult.LightTransmittance,
                    uf)
                {
                    ApertureConstruction = apertureConstruction,
                    MaterialLibrary = constructionManager.MaterialLibrary,
                });
            }

            return result;
        }
    }
}
