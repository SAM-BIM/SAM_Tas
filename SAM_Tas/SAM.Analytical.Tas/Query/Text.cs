// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static string Text(this PanelDataType panelDataType)
        {
            return Core.Query.Description(panelDataType);
        }

        public static string Text(this SpaceDataType spaceDataType)
        {
            return Core.Query.Description(spaceDataType);
        }
    }
}
