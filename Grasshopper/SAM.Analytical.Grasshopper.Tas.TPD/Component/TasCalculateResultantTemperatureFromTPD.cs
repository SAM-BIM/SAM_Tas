// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SAM.Analytical.Grasshopper.Tas.TPD.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Analytical.Grasshopper.Tas.TPD
{
    public class TasCalculateResultantTemperatureFromTPD : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new ("ec0be2d6-e393-4c6a-8213-f96f24fc3211");

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
        public TasCalculateResultantTemperatureFromTPD()
          : base("Tas.CalculateResultantTemperatureFromTPD", "Tas.CalculateResultantTemperatureFromTPD",
              "Runs a follow-up Tas simulation from a TPD file to calculate resultant temperatures. The component copies the matching TBD, writes TPD zone-temperature profiles into zone thermostat limits, simulates the copied model, and returns the generated TSD and TBD paths.",
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
                List<GH_SAMParam> result = [];
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_path_TPD", NickName = "_path_TPD", Description = "Path to the source Tas TPD file. A TBD file with the same name must exist in the same folder.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean  @boolean = new() { Name = "_run", NickName = "_run", Description = "Set to True to create the thermostat-driven TBD copy and run the Tas simulation.", Access = GH_ParamAccess.item };
                @boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(@boolean, ParamVisibility.Binding));

                return [.. result];
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = [];
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "path_TSD", NickName = "path_TSD", Description = "Path to the generated Tas TSD results file.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "path_TBD", NickName = "path_TBD", Description = "Path to the generated Tas TBD model copy with thermostat profiles from the TPD results.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean @boolean = new() { Name = "successful", NickName = "successful", Description = "True when the TPD was converted, the copied TBD was simulated, and the TSD file was unlocked after simulation.", Access = GH_ParamAccess.item };
                @boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(@boolean, ParamVisibility.Binding));

                return [.. result];
            }
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="dataAccess">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index_successful = Params.IndexOfOutputParam("successful");
            if(index_successful != -1)
            {
                dataAccess.SetData(index_successful, false);
            }

            int index;

            bool run = false;
            index = Params.IndexOfInputParam("_run");
            if (index == -1 || !dataAccess.GetData(index, ref run))
            {
                run = false;
            }

            if (!run)
            {
                return;
            }

            string path_TPD = null;
            index = Params.IndexOfInputParam("_path_TPD");
            if (index == -1 || !dataAccess.GetData(index, ref path_TPD) || string.IsNullOrWhiteSpace(path_TPD))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            bool successful = Analytical.Tas.TPD.Modify.CalculateResultantTemperature(path_TPD, out string path_TBD, out string path_TSD);

            if (index_successful != -1)
            {
                dataAccess.SetData(index_successful, successful);
            }

            index = Params.IndexOfOutputParam("path_TBD");
            if (index != -1)
            {
                dataAccess.SetData(index, path_TBD);
            }

            index = Params.IndexOfOutputParam("path_TSD");
            if (index != -1)
            {
                dataAccess.SetData(index, path_TSD);
            }
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);

            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Open TSD", Menu_OpenTSD, Resources.SAM_TasTSD3, true, false);
            Menu_AppendItem(menu, "Open TPD", Menu_OpenTPD, Resources.SAM_TasTPD3, true, false);
            Menu_AppendItem(menu, "Open TBD", Menu_OpenTBD, Resources.SAM_TasTBD3, true, false);
        }

        private void Menu_OpenTSD(object sender, EventArgs e)
        {
            Open("path_TSD");
        }

        private void Menu_OpenTPD(object sender, EventArgs e)
        {
            Open("_path_TPD");
        }

        private void Menu_OpenTBD(object sender, EventArgs e)
        {
            Open("path_TBD");
        }

        private void Open(string parameterName)
        {
            object @object = null;

            int index_Path = Params.IndexOfInputParam(parameterName);
            if (index_Path != -1)
            {
                @object = Params.Input[index_Path].VolatileData.AllData(true)?.OfType<object>()?.ElementAt(0);
            }

            if (@object == null)
            {
                index_Path = Params.IndexOfOutputParam(parameterName);
                if (index_Path != -1)
                {
                    @object = Params.Output[index_Path].VolatileData.AllData(true)?.OfType<object>()?.ElementAt(0);
                }
            }

            string path = null;
            if (@object is IGH_Goo)
            {
                path = (@object as dynamic).Value?.ToString();
            }

            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            Core.Query.StartProcess(path);
        }
    }
}
