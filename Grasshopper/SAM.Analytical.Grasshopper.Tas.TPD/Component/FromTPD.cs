﻿using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Tas.TPD.Properties;
using SAM.Analytical.Tas.TPD;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Tas.TPD
{
    public class FromTPD : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("c88b33fb-5c8f-442e-bd43-d9a2809c2bc2");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.1";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_TasTPD;


        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public FromTPD()
          : base("SAMAnalytical.FromTPD", "SAMAnalytical.FromTPD",
              "Converts from TPD to SystemModel",
              "SAM WIP", "Tas")
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_FilePath() { Name = "_path_TPD", NickName = "_path_TPD", Description = "A file path to TAS TPD", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean @boolean = null;

                SystemModelConversionSettings systemModelConversionSettings = new SystemModelConversionSettings();

                @boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_simulate_", NickName = "_simulate_", Description = "Simulate before collecting data", Access = GH_ParamAccess.item };
                @boolean.SetPersistentData(systemModelConversionSettings.Simulate);
                result.Add(new GH_SAMParam(@boolean, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Integer integer = null;

                integer = new global::Grasshopper.Kernel.Parameters.Param_Integer() { Name = "_startHour_", NickName = "_startHour_", Description = "Simulation start hour", Access = GH_ParamAccess.item };
                integer.SetPersistentData(systemModelConversionSettings.StartHour);
                result.Add(new GH_SAMParam(integer, ParamVisibility.Voluntary));

                integer = new global::Grasshopper.Kernel.Parameters.Param_Integer() { Name = "_endHour_", NickName = "_endHour_", Description = "Simulation end hour", Access = GH_ParamAccess.item };
                integer.SetPersistentData(systemModelConversionSettings.EndHour);
                result.Add(new GH_SAMParam(integer, ParamVisibility.Voluntary));

                @boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_run", NickName = "_run", Description = "Connect a boolean toggle to run.", Access = GH_ParamAccess.item };
                @boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(@boolean, ParamVisibility.Binding));

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
                result.Add(new GH_SAMParam(new GooSystemModelParam() { Name = "systemModel", NickName = "systemModel", Description = "SAM Analytical SystemModel", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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
            int index_successful = Params.IndexOfOutputParam("successful");
            if (index_successful != -1)
            {
                dataAccess.SetData(index_successful, false);
            }

            int index;

            bool run = false;
            index = Params.IndexOfInputParam("_run");
            if (index == -1 || !dataAccess.GetData(index, ref run))
                run = false;

            if (!run)
            {
                return;
            }

            SystemModelConversionSettings systemModelConversionSettings = new SystemModelConversionSettings();

            bool simulate = systemModelConversionSettings.Simulate;
            index = Params.IndexOfInputParam("_simulate_");
            if (index != -1 && dataAccess.GetData(index, ref simulate))
            {
                systemModelConversionSettings.Simulate = simulate;
            }

            int startHour = systemModelConversionSettings.StartHour;
            index = Params.IndexOfInputParam("_startHour_");
            if (index != -1 && dataAccess.GetData(index, ref startHour))
            {
                systemModelConversionSettings.StartHour = startHour;
            }

            int endHour = systemModelConversionSettings.EndHour;
            index = Params.IndexOfInputParam("_endHour_");
            if (index != -1 && dataAccess.GetData(index, ref endHour))
            {
                systemModelConversionSettings.EndHour = endHour;
            }

            string path = null;
            index = Params.IndexOfInputParam("_path_TPD");
            if (index == -1 || !dataAccess.GetData(index, ref path) || string.IsNullOrWhiteSpace(path))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            SystemModel systemModel = Analytical.Tas.TPD.Convert.ToSAM(path, systemModelConversionSettings);

            index = Params.IndexOfOutputParam("systemModel");
            if (index != -1)
                dataAccess.SetData(index, new GooSystemModel(systemModel));

            if (index_successful != -1)
            {
                dataAccess.SetData(index_successful, systemModel != null);
            }
        }
    }
}