// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool RemoveMaterials(this TBD.Construction construction)
        {
            if (construction == null)
                return false;

            TBD.material material = construction.materials(1);
            while (material != null)
            {
                construction.RemoveMaterial(0);
                material = construction.materials(1);
            }

            return true;
        }
    }
}
