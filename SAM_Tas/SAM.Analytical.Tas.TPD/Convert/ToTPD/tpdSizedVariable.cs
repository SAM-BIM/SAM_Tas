// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static tpdSizedVariable ToTPD(this SizingType sizingType)
        {
            switch (sizingType)
            {
                case SizingType.None:
                    return tpdSizedVariable.tpdSizedVariableNone;

                case SizingType.Sized:
                    return tpdSizedVariable.tpdSizedVariableSize;

                case SizingType.Value:
                    return tpdSizedVariable.tpdSizedVariableValue;
            }

            throw new System.NotImplementedException();
        }
    }
}
