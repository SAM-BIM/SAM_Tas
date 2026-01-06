// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static TBD.material AddMaterial(this TBD.Construction construction, Core.Material material)
        {
            if (construction == null || material == null)
                return null;

            TBD.material result = construction.AddMaterial();
            if (result == null)
                return result;

            result.UpdateMaterial(material);

            return result;
        }
  }
}
