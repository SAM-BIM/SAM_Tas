// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static List<ConstructionLayer> ConstructionLayers(this TBD.Construction construction)
        {
            List<ConstructionLayer> result = new List<ConstructionLayer>();

            int index = 1;
            TBD.material material = construction.materials(index);
            while (material != null)
            {
                ConstructionLayer constructionLayer = new ConstructionLayer(material.name, construction.materialWidth[index]); 
                
                result.Add(constructionLayer);
                index++;

                material = construction.materials(index);
            }

            return result;
        }
    }
}
