// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static string Sufix(this AperturePart aperturePart)
        {
            switch(aperturePart)
            {
                case Analytical.AperturePart.Frame:
                    return "-frame";

                case Analytical.AperturePart.Pane:
                    return "-pane";
            }

            return null;
        }
    }
}
