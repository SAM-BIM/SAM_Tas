﻿using SAM.Analytical.Systems;
using SAM.Core;
using SAM.Core.Systems;
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using SAM.Geometry.Systems;
using System.Collections.Generic;
using System.Linq;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Modify
    {
        public static bool Update(this ProfileData profileData, ModifiableValue modifiableValue)
        {
            if(profileData == null || modifiableValue == null)
            {
                return false;
            }

            profileData.Value = modifiableValue.Value;
            profileData.ClearModifiers();
            profileData.AddModifier(modifiableValue.Modifier);

            return true;
        }

        public static bool Update(this SizedVariable sizedVariable, ISizableValue sizableValue, PlantRoom plantRoom)
        {
            return Update(sizedVariable, sizableValue, plantRoom?.GetEnergyCentre());
        }

        public static bool Update(this SizedVariable sizedVariable, ISizableValue sizableValue, global::TPD.System system)
        {
            return Update(sizedVariable, sizableValue, system?.GetPlantRoom()?.GetEnergyCentre());
        }


        public static bool Update(this SizedVariable sizedVariable, ISizableValue sizableValue, EnergyCentre energyCentre)
        {
            if (sizableValue == null || sizedVariable == null)
            {
                return false;
            }

            dynamic @dynamic = sizedVariable;

            if(sizableValue is UnlimitedValue)
            {
                sizedVariable.Type = tpdSizedVariable.tpdSizedVariableNone;
                return true;
            }


            if(sizableValue is SizableValue)
            {
                SizableValue sizableValue_Temp = (SizableValue)sizableValue;

                sizedVariable.Type = sizableValue_Temp.SizingType.ToTPD();
                sizedVariable.Method = sizableValue_Temp.SizeMethod.ToTPD();
                sizedVariable.SizeFraction = sizableValue_Temp.SizeFraction;

                ModifiableValue modifiableValue = sizableValue_Temp.ModifiableValue;
                if(modifiableValue != null)
                {
                    dynamic.Value = modifiableValue.Value;
                }
            }
            
            if(sizedVariable is DesignConditionSizableValue && energyCentre != null)
            {
                DesignConditionSizableValue designConditionSizableValue = (DesignConditionSizableValue)sizedVariable;
                HashSet<string> designConditionNames = designConditionSizableValue.DesignConditionNames;
                if(designConditionNames != null)
                {
                    foreach(string designConditionName in designConditionNames)
                    {
                        DesignConditionLoad designConditionLoad = energyCentre.DesignConditionLoad(designConditionName);
                        if(designConditionLoad != null)
                        {
                            sizedVariable.AddDesignCondition(designConditionLoad);
                        }
                    }
                }
            }

            return true;
        }

        public static bool Update(this SizedFlowVariable sizedFlowVariable, SizedFlowValue sizedFlowValue)
        {
            if (sizedFlowVariable == null || sizedFlowValue == null)
            {
                return false;
            }

            sizedFlowVariable.Value = sizedFlowValue.Value;
            sizedFlowVariable.SizeFraction = sizedFlowValue.SizeFranction;

            return true;
        }

        public static bool Update(this ControllerProfileData controllerProfileData, GFunction gFunction)
        {
            if (controllerProfileData == null || gFunction == null)
            {
                return false;
            }

            return true;
        }

    }
}