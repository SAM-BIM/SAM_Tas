// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Create
    {
        public static ParameterFile ParameterFile(this CommandFile commandFile)
        {
            if(commandFile == null)
            {
                return null;
            }

            ParameterFile result = new ParameterFile(commandFile.Parameters);

            return result;
        }
    }
}
