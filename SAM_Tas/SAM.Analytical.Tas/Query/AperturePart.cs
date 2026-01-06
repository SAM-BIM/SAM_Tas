// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static AperturePart AperturePart(this int bEType)
        {
            switch(bEType)
            {
                case 14:
                    return Analytical.AperturePart.Frame;
                case 12:
                    return Analytical.AperturePart.Pane;
                case 13:
                    return Analytical.AperturePart.Pane;
                case 15:
                    return Analytical.AperturePart.Frame;
            }

            return Analytical.AperturePart.Undefined;
        }
    }
}
