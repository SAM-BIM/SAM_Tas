// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.SAP
{
    public static partial class Query
    {
        public static BuildingElementType BuildingElemetType(this PanelType panelType)
        { 
            switch(panelType.PanelGroup())
            {
                case PanelGroup.Floor:
                    return BuildingElementType.Floor;

                case PanelGroup.Wall:
                    return BuildingElementType.Wall;

                case PanelGroup.Roof:
                    return BuildingElementType.Roof;
            }

            return BuildingElementType.Undefined;
        }
    }
}
