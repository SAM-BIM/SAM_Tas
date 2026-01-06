// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static T Component<T>(this TPD.PlantRoom plantRoom, string name)
        {
            if (plantRoom is null || string.IsNullOrWhiteSpace(name))
            {
                return default;
            }

            for (int i = 1; i <= plantRoom.GetComponentCount(); i++)
            {
                TPD.PlantComponent plantComponent = plantRoom.GetComponent(i);
                if(!(plantComponent is T))
                {
                    continue;
                }

                if(name.Equals((plantComponent as dynamic).Name))
                {
                    return (T)plantComponent;
                }
            }

            return default;
        }
    }
}
