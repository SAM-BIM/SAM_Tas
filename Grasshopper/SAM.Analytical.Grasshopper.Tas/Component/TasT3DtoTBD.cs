// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Tas.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Tas
{
    public class TasT3DtoTBD : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("85b9fea2-d977-49c4-8295-8f207d10e13f");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.3";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_TasTBD3;

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public TasT3DtoTBD()
          : base("Tas.T3DtoTBD", "Tas.T3DtoTBD",
              "Exports/Converts TasT3D to TBD.",
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_pathTasT3D", NickName = "_pathTasT3D", Description = "The string path to a TasT3D file.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_pathTasTBD", NickName = "_pathTasTBD", Description = "The string path to a TasTBD file.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Integer param_Integer = new global::Grasshopper.Kernel.Parameters.Param_Integer() { Name = "_dayFirst_", NickName = "_dayFirst_", Description = "The first day", Access = GH_ParamAccess.item };
                param_Integer.SetPersistentData(1);
                result.Add(new GH_SAMParam(param_Integer, ParamVisibility.Binding));

                param_Integer = new global::Grasshopper.Kernel.Parameters.Param_Integer() { Name = "_dayLast_", NickName = "_dayLast_", Description = "The last day", Access = GH_ParamAccess.item };
                param_Integer.SetPersistentData(365);
                result.Add(new GH_SAMParam(param_Integer, ParamVisibility.Binding));

                param_Integer = new global::Grasshopper.Kernel.Parameters.Param_Integer() { Name = "_step_", NickName = "_step_", Description = "What should the time interval (in days) be, of the solar calculations?", Access = GH_ParamAccess.item };
                param_Integer.SetPersistentData(15);
                result.Add(new GH_SAMParam(param_Integer, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_autoAssignConstructions_", NickName = "_autoAssignConstructions_", Description = "Should the construction be assigned automatically?", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(true);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_removeTBD", NickName = "_removeTBD", Description = "If True existing TBD file will be deleted before simulation", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(false);
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
                return;

            index = Params.IndexOfInputParam("_pathTasT3D");
            string path_T3D = null;
            if (index == -1 || !dataAccess.GetData(index, ref path_T3D) || string.IsNullOrWhiteSpace(path_T3D))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_pathTasTBD");
            string path_TBD = null;
            if (index == -1 || !dataAccess.GetData(index, ref path_TBD) || string.IsNullOrWhiteSpace(path_TBD))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_dayFirst_");
            int day_First = int.MinValue;
            if (index == -1 || !dataAccess.GetData(index, ref day_First))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_dayLast_");
            int day_Last = int.MinValue;
            if (index == -1 || !dataAccess.GetData(index, ref day_Last))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_step_");
            int step = int.MinValue;
            if (index == -1 || !dataAccess.GetData(index, ref step))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_autoAssignConstructions_");
            bool autoAssignConstructions = true;
            if (index == -1 || !dataAccess.GetData(index, ref autoAssignConstructions))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_removeTBD");
            bool removeExisting = true;
            if (index == -1 || !dataAccess.GetData(index, ref removeExisting))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }


            bool result = Analytical.Tas.Convert.ToTBD(path_T3D, path_TBD, day_First, day_Last, step, autoAssignConstructions, removeExisting);

            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, result);
            }
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            bool result = base.Read(reader);
            Core.Grasshopper.Tas.Modify.ReadLegacyParameterData(this, reader, Inputs, Outputs);
            return result;
        }
    }
}
