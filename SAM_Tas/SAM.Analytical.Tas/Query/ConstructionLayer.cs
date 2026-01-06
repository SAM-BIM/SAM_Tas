// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static ConstructionLayer ConstructionLayer(this LayerThicknessCalculationResult layerThicknessCalculationResult, ConstructionManager constructionManager)
        {
            if (layerThicknessCalculationResult == null || constructionManager == null)
            {
                return null;
            }

            Construction construction = Construction(layerThicknessCalculationResult, constructionManager);
            if(construction == null)
            {
                return null;
            }

            List<ConstructionLayer> constructionLayers = construction.ConstructionLayers;
            if (constructionLayers == null || constructionLayers.Count == 0)
            {
                return null;
            }

            if (constructionLayers.Count <= layerThicknessCalculationResult.LayerIndex)
            {
                return null;
            }

            return constructionLayers[layerThicknessCalculationResult.LayerIndex];
        }
    }
}
