// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Tas.Properties;
using SAM.Analytical.Tas;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Tas
{
    public class TasSizing : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("704f954c-7ee1-41b8-ac85-3bf776ca4f36");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.4";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_TasTBD3;

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public TasSizing()
          : base("Tas.Sizing", "Tas.Sizing",
              "Sizes the TasTBD file.",
              "SAM", "Tas")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();

                global::Grasshopper.Kernel.Parameters.Param_String param_String = new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_pathTasTBD", NickName = "_pathTasTBD", Description = "The string path to a TasTBD file.", Access = GH_ParamAccess.item };
                param_String.WireDisplay = GH_ParamWireDisplay.hidden;
                result.Add(new GH_SAMParam(param_String, ParamVisibility.Binding));

                result.Add(new GH_SAMParam(new GooAnalyticalModelParam() { Name = "_analyticalModel_", NickName = "_analyticalModel_", Description = "A SAM analytical model", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_excludeOutdoorAir_", NickName = "_excludeOutdoorAir_", Description = "Should the outdoor air be excluded and TBD ventilation factor set to zero?", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_excludePositiveInternalGains_", NickName = "_excludePositiveInternalGains_", Description = "Should the internal conduction gains not offset the heating load from adjacent rooms? This will re-run few times, sizing the simulation for each temperature.", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(true);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_systemSizingMethod_", NickName = "_systemSizingMethod_", Description = "System Sizing Method", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_generateUncappedFile_", NickName = "_generateUncappedFile_", Description = "Generate Uncapped File", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(true);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_generateHDDCDDFile_", NickName = "_generateHDDCDDFile_", Description = "Generate HDD and CDD File", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(true);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_run", NickName = "_run", Description = "Connect a boolean toggle to run.", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                return result.ToArray();
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "successful", NickName = "successful", Description = "Correctly imported?", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                return result.ToArray();
            }
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="dataAccess">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index = -1;

            int index_Successful = Params.IndexOfOutputParam("successful");
            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, false);
            }

            index = Params.IndexOfInputParam("_run");
            bool run = false;
            if (index == -1 || !dataAccess.GetData(index, ref run))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            if (!run)
            {
                return;
            }

            index = Params.IndexOfInputParam("_analyticalModel_");
            AnalyticalModel analyticalModel = null;
            if (index != -1)
            {
                if (!dataAccess.GetData(index, ref analyticalModel))
                {
                    analyticalModel = null;
                }
            }

            index = Params.IndexOfInputParam("_pathTasTBD");
            string path_TBD = null;
            if (index == -1 || !dataAccess.GetData(index, ref path_TBD) || string.IsNullOrWhiteSpace(path_TBD))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_excludeOutdoorAir_");
            bool excludeOutdoorAir = false;
            if (index == -1 || !dataAccess.GetData(index, ref excludeOutdoorAir))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_excludePositiveInternalGains_");
            bool excludePositiveInternalGains = true;
            if (index == -1 || !dataAccess.GetData(index, ref excludePositiveInternalGains))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_systemSizingMethod_");
            bool systemSizingMethod = false;
            if (index == -1 || !dataAccess.GetData(index, ref systemSizingMethod))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_generateUncappedFile_");
            bool generateUncappedFile = true;
            if (index == -1 || !dataAccess.GetData(index, ref generateUncappedFile))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_generateHDDCDDFile_");
            bool generateHDDCDDFile = true;
            if (index == -1 || !dataAccess.GetData(index, ref generateHDDCDDFile))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            SizingSettings sizingSettings = new SizingSettings() 
            { 
                ExcludeOutdoorAir = excludeOutdoorAir, 
                ExcludePositiveInternalGains = excludePositiveInternalGains,
                SystemSizingMethod = systemSizingMethod,
                GenerateUncappedFile = generateUncappedFile,
                GenerateHDDCDDFile = generateHDDCDDFile
            };

            bool result = Analytical.Tas.Query.Sizing(path_TBD, sizingSettings, analyticalModel);

            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, result);
            }
        }
    }
}
