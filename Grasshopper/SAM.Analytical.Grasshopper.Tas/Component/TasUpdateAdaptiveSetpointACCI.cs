// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Tas.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Tas
{
    public class TasUpdateAdaptiveSetpointACCI : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("12c19e3e-6cf1-4112-b7a4-0c4bcaa408e2");

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
        public TasUpdateAdaptiveSetpointACCI()
          : base("Tas.UpdateAdaptiveSetpointACCI", "Tas.UpdateAdaptiveSetpointACCI",
        "https://accim.readthedocs.io/en/latest/4_detailed%20use.html\n" +
        "https://github.com/dsanchez-garcia/accim/blob/master/accim/sample_files/jupyter_notebooks/addAccis/using_addAccis.ipynb\n" +
        "Adopted 2->INT ASHRAE55->90->3->Adap. Limits   Horizont. Extended\n" +
        "https://htmlpreview.github.io/?https://github.com/dsanchez-garcia/accim/blob/master/accim/docs/html_files/full_setpoint_table.html\n\n" +
        "1. Input Parameter:\n" +
        "   - dryBulbTemperature (External Weather Data): The dry bulb temperature from the weather data, which is used to calculate the upper and lower limits.\n" +
        "\n" +
        "2. Conditions:\n" +
        "   - Within Range (10°C to 33.5°C):\n" +
        "     - If the dryBulbTemperature is between 10°C and 33.5°C, inclusive, the following equations are used to calculate the upper and lower limits:\n" +
        "       - upperLimit = (dryBulbTemperature * 0.31) + 17.8 + 3.5\n" +
        "       - lowerLimit = (dryBulbTemperature * 0.31) + 17.8 - 3.5\n" +
        "   - Below Range (< 10°C):\n" +
        "     - If the dryBulbTemperature is below 10°C, a fixed dry bulb temperature of 10°C is used in the equations to calculate the upper and lower limits:\n" +
        "       - upperLimit = (10 * 0.31) + 17.8 + 3.5\n" +
        "       - lowerLimit = (10 * 0.31) + 17.8 - 3.5\n" +
        "   - Above Range (> 33.5°C):\n" +
        "     - If the dryBulbTemperature is above 33.5°C, a fixed dry bulb temperature of 33.5°C is used in the equations to calculate the upper and lower limits:\n" +
        "       - upperLimit = (33.5 * 0.31) + 17.8 + 3.5\n" +
        "       - lowerLimit = (33.5 * 0.31) + 17.8 - 3.5\n" +
        "\n" +
        "3. Output Parameters:\n" +
        "   - upperLimit: The calculated upper limit value.\n" +
        "   - lowerLimit: The calculated lower limit value.",
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_pathTasTBD", NickName = "pathTasTBD", Description = "The string path to TasTBD file.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSpaceParam() { Name = "spaces_", NickName = "spaces_", Description = "SAM Analytical Spaces", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_run", NickName = "_run", Description = "Connect a boolean toggle to run.", Access = GH_ParamAccess.item };
                boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(boolean, ParamVisibility.Binding));

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

            index = Params.IndexOfInputParam("_pathTasTBD");
            string path_TBD = null;
            if (index == -1 || !dataAccess.GetData(index, ref path_TBD) || string.IsNullOrWhiteSpace(path_TBD))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("spaces_");
            List<Space> spaces = new List<Space>();
            if (index != -1)
            {
                dataAccess.GetDataList(index, spaces);
            }
            if (spaces.Count == 0)
            {
                spaces = null;
            }

            bool result = Analytical.Tas.Modify.UpdateACCI(path_TBD, spaces?.ConvertAll(x => x?.Name));

            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, result);
            }
        }
    }
}
