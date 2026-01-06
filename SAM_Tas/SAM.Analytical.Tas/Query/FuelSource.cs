// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static TPD.FuelSource FuelSource(this TPD.EnergyCentre energyCentre, string name)
        {
            if (energyCentre is null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            for (int i = 1; i <= energyCentre.GetFuelSourceCount(); i++)
            {
                TPD.FuelSource fuelSource = energyCentre.GetFuelSource(i);
                if(fuelSource == null)
                {
                    continue;
                }

                if(name.Equals((fuelSource as dynamic).Name))
                {
                    return fuelSource;
                }
            }

            return null;
        }
    }
}
