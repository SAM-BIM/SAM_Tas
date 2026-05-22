// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SAM.Analytical.Grasshopper.Tas.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Analytical.Grasshopper.Tas
{
    public class SAMAnalyticalFromTBD : GH_SAMComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("9962a735-34b0-4e5c-8647-285d5baaab62");

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
        public SAMAnalyticalFromTBD()
          : base("SAMAnalytical.FromTBD", "SAMAnalytical.FromTBD",
              "Creates a SAM AnalyticalModel by reading the geometry, constructions, and building data from a TasTBD file.\nWith _importSurfaceShades_ = true, also extracts TAS-computed shade-proportion data (per zoneSurface × shade-day × hour) and attaches it to the model as a SolarModel under AnalyticalModelParameter.SolarModel — enabling apples-to-apples comparison against SAM's own solar engine via SAMAnalytical.CompareSolarCoverage.",
              "SAM", "Tas")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager inputParamManager)
        {
            //int aIndex = -1;
            //Param_Boolean booleanParameter = null;

            // Input order is preserved from the previous public release — new optional inputs
            // are appended AFTER _run so existing Grasshopper definitions wired against the
            // 3-input (_pathTasTBD / _importUnused_ / _run) signature keep their wires
            // correctly mapped instead of having _run silently shift right.
            inputParamManager.AddTextParameter("_pathTasTBD", "_pathTasTBD", "The string path to a TasTBD file.", GH_ParamAccess.item);
            inputParamManager.AddBooleanParameter("_importUnused_", "_importUnused_", "Import Unused IC", GH_ParamAccess.item, false);
            inputParamManager.AddBooleanParameter("_run", "_run", "Connect a boolean toggle to run.", GH_ParamAccess.item, false);
            inputParamManager.AddBooleanParameter("_importSurfaceShades_", "_importSurfaceShades_", "If true, reads TAS-computed shade proportions for every exposed zoneSurface and attaches a populated SolarModel to the AnalyticalModel (via AnalyticalModelParameter.SolarModel). Required input for SAMAnalytical.CompareSolarCoverage. Adds a few seconds to import time.", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager outputParamManager)
        {
            outputParamManager.AddParameter(new GooAnalyticalModelParam(), "analyticalModel", "analyticalModel", "SAM AnalyticalModel", GH_ParamAccess.list);
            outputParamManager.AddBooleanParameter("successful", "successful", "Correctly imported?", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="dataAccess">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            dataAccess.SetData(1, false);

            // Input indices: 0 = _pathTasTBD, 1 = _importUnused_, 2 = _run, 3 = _importSurfaceShades_
            bool run = false;
            if (!dataAccess.GetData(2, ref run))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }
            if (!run)
                return;

            string path_TBD = null;
            if (!dataAccess.GetData(0, ref path_TBD) || string.IsNullOrWhiteSpace(path_TBD))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            bool importUnused = false;
            if (!dataAccess.GetData(1, ref importUnused))
            {
                importUnused = false;
            }

            bool importSurfaceShades = false;
            if (!dataAccess.GetData(3, ref importSurfaceShades))
            {
                importSurfaceShades = false;
            }

            AnalyticalModel analyticalModel = Analytical.Tas.Convert.ToSAM(path_TBD, importUnused, importSurfaceShades);

            dataAccess.SetData(0, analyticalModel);
            dataAccess.SetData(1, analyticalModel != null);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);

            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Open TBD", Menu_OpenTBD, Resources.SAM_TasTBD3, true, false);
        }

        private void Menu_OpenTBD(object sender, EventArgs e)
        {
            int index_Path = Params.IndexOfInputParam("_pathTasTBD");
            if (index_Path == -1)
            {
                return;
            }

            string path = null;

            object @object = null;

            @object = Params.Input[index_Path].VolatileData.AllData(true)?.OfType<object>()?.ElementAt(0);
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