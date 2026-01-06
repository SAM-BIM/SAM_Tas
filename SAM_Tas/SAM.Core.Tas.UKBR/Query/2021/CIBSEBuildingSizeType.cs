// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Core.Tas.UKBR.v2021
{
    public static partial class Query
    {
        public static CIBSEBuildingSizeType? CIBSEBuildingSizeType(this Building building)
        {
            return Core.Query.Enum<CIBSEBuildingSizeType>(building.CIBSEBuildingSizeIndex);
        }
    }
}
