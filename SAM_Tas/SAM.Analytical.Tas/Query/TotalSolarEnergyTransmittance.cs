// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static double TotalSolarEnergyTransmittance(this TBD.buildingElement buildingElement, double tolerance = Core.Tolerance.MacroDistance)
        {
            return TotalSolarEnergyTransmittance(buildingElement?.GetConstruction(), tolerance);
        }

        public static double TotalSolarEnergyTransmittance(this TBD.Construction construction, double tolerance = Core.Tolerance.MacroDistance)
        {
            if (construction == null)
                return double.NaN;

            TBD.ConstructionTypes constructionTypes = construction.type;
            if (constructionTypes == TBD.ConstructionTypes.tcdTransparentConstruction)
            {
                object @object = construction.GetGlazingValues();
                float[] values = Array<float>(@object);
                if (values == null || values.Length <= 5)
                    return double.NaN;

                return Core.Query.Round(values[5], tolerance);
            }
            return 0;
        }
    }
}
