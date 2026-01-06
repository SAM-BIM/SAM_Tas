// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool RemoveFeatureShades(this TBD.buildingElement buildingElement)
        {
            if(buildingElement is null)
            {
                return false;
            }

            bool result = false;

            bool removed = false;

            do
            {
                removed = false;
                TBD.FeatureShade featureShade = buildingElement.GetFeatureShade(1);
                if (!(featureShade is null))
                {
                    buildingElement.RemoveFeatureShade(1);
                    removed = true;
                    result = true;
                }
            }
            while (removed);

            return result;
        }
    }
}
