﻿using SAM.Core;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Query
    {
        public static List<global::TPD.Duct> Ducts(global::TPD.ISystemComponent systemComponent, Direction direction)
        {
            if(systemComponent == null)
            {
                return null;
            }

            int portCount = direction == Direction.Out ? (systemComponent as dynamic).GetOutputPortCount() : (systemComponent as dynamic).GetInputPortCount();
            if(portCount < 1)
            {
                return null;
            }

            List<global::TPD.Duct> result = new List<global::TPD.Duct>();
            for(int i = 1; i <= portCount; i ++)
            {
                int ductCount = direction == Direction.Out ? (systemComponent as dynamic).GetOutputDuctCount(i) : (systemComponent as dynamic).GetInputDuctCount(i);
                if(ductCount < 1)
                {
                    continue;
                }

                for (int j = 1; j <= ductCount; j++)
                {
                    global::TPD.Duct duct = direction == Direction.Out ? (systemComponent as dynamic).GetOutputDuct(i, j) : (systemComponent as dynamic).GetInputDuct(i, j);

                    if(duct == null)
                    {
                        continue;
                    }

                    result.Add(duct);
                }

            }

            return result;
        }

        public static List<global::TPD.Duct> Ducts(global::TPD.System system)
        {
            if (system == null)
            {
                return null;
            }

            int ductCount = system.GetDuctCount();
            if (ductCount < 1)
            {
                return null;
            }

            List<global::TPD.Duct> result = new List<global::TPD.Duct>();
            for (int i = 1; i <= ductCount; i++)
            {
                global::TPD.Duct duct = system.GetDuct(i);
                if (duct == null)
                {
                    continue;
                }

                result.Add(duct);
            }

            return result;
        }

        public static List<global::TPD.Duct> Ducts(global::TPD.ComponentGroup componentGroup)
        {
            if (componentGroup == null)
            {
                return null;
            }

            int ductCount = componentGroup.GetDuctCount();
            if (ductCount < 1)
            {
                return null;
            }

            List<global::TPD.Duct> result = new List<global::TPD.Duct>();
            for (int i = 1; i <= ductCount; i++)
            {
                global::TPD.Duct duct = componentGroup.GetDuct(i);
                if (duct == null)
                {
                    continue;
                }

                result.Add(duct);
            }

            return result;
        }
    }
}