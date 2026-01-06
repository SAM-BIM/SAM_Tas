// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TPD;
using SAM.Analytical.Systems;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static SetpointMode ToSAM(this tpdSetpointMethod tpdSetpointMethod)
        {
            switch(tpdSetpointMethod)
            {
                case tpdSetpointMethod.tpdSetpointNone:
                    return SetpointMode.None;

                case tpdSetpointMethod.tpdSetpointOn:
                    return SetpointMode.On;
            }

            throw new System.NotImplementedException();
        }
    }
}
