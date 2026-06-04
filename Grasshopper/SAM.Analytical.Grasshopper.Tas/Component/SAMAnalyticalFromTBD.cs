// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SAM.Analytical.Grasshopper.Tas.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Analytical.Grasshopper.Tas
{
    public class SAMAnalyticalFromTBD : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("9962a735-34b0-4e5c-8647-285d5baaab62");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.6";

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
        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = [];
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_pathTasTBD", NickName = "_pathTasTBD", Description = "The string path to a TasTBD file.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean @boolean = null;

                @boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_importUnused_", NickName = "_importUnused_", Description = "Import Unused IC", Access = GH_ParamAccess.item };
                @boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(@boolean, ParamVisibility.Binding));

                @boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_importSurfaceShades_", NickName = "_importSurfaceShades_", Description = "If true, reads TAS-computed shade proportions for every exposed zoneSurface and attaches a populated SolarModel to the AnalyticalModel (via AnalyticalModelParameter.SolarModel). Required input for SAMAnalytical.CompareSolarCoverage. Adds a few seconds to import time.", Access = GH_ParamAccess.item };
                @boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(@boolean, ParamVisibility.Binding));

                @boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_run", NickName = "_run", Description = "Connect a boolean toggle to run.", Access = GH_ParamAccess.item };
                @boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(@boolean, ParamVisibility.Binding));

                @boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_debug_", NickName = "_debug_", Description = "If true, writes a per-run diagnostic of the SolarModel→AnalyticalModel result copy (which TAS surface each aperture pane/frame and panel matched, and why any was skipped) to %TEMP%\\SAM_CopyResults.log, and returns its text on the 'debugLog' output. Off by default.", Access = GH_ParamAccess.item };
                @boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(@boolean, ParamVisibility.Voluntary));

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
                result.Add(new GH_SAMParam(new GooAnalyticalModelParam() { Name = "analyticalModel", NickName = "analyticalModel", Description = "SAM Analytical Model", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "pathTasTBD", NickName = "pathTasTBD", Description = "The string path to a TasTBD file.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "successful", NickName = "successful", Description = "Correctly imported?", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "debugLog", NickName = "debugLog", Description = "Diagnostic log for the pane/frame/panel result matching. Populated only when _debug_ is true (otherwise empty).", Access = GH_ParamAccess.item }, ParamVisibility.Voluntary));
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
            if (index_successful != -1)
            {
                dataAccess.SetData(index_successful, false);
            }

            int index;

            // Input indices: 0 = _pathTasTBD, 1 = _importUnused_, 2 = _run, 3 = _importSurfaceShades_
            bool run = false;
            index = Params.IndexOfInputParam("_run");
            if (!dataAccess.GetData(index, ref run))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            if (!run)
            {
                return;
            }

            string path_TBD = null;
            index = Params.IndexOfInputParam("_pathTasTBD");
            if (!dataAccess.GetData(index, ref path_TBD) || string.IsNullOrWhiteSpace(path_TBD))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            bool importUnused = false;
            index = Params.IndexOfInputParam("_importUnused_");
            if (!dataAccess.GetData(index, ref importUnused))
            {
                importUnused = false;
            }

            bool importSurfaceShades = false;
            index = Params.IndexOfInputParam("_importSurfaceShades_");
            if (!dataAccess.GetData(index, ref importSurfaceShades))
            {
                importSurfaceShades = false;
            }

            bool debug = false;
            index = Params.IndexOfInputParam("_debug_");
            if (!dataAccess.GetData(index, ref debug))
            {
                debug = false;
            }

            // Toggle the CopyResults diagnostic log (Modify.CopyResults reads SAM_DEBUG) for this
            // Rhino process. Setting it to null clears it, so unchecking _debug_ turns logging off.
            Environment.SetEnvironmentVariable("SAM_DEBUG", debug ? "1" : null);

            AnalyticalModel analyticalModel = Analytical.Tas.Convert.ToSAM(path_TBD, importUnused, importSurfaceShades);

            index = Params.IndexOfOutputParam("analyticalModel");
            if(index != -1)
            {
                dataAccess.SetData(index, analyticalModel);
            }

            index = Params.IndexOfOutputParam("pathTasTBD");
            if (index != -1)
            {
                dataAccess.SetData(index, path_TBD);
            }

            index = Params.IndexOfOutputParam("successful");
            if (index != -1)
            {
                dataAccess.SetData(index, analyticalModel != null);
            }

            index = Params.IndexOfOutputParam("debugLog");
            if (index != -1)
            {
                string debugLog = null;
                if (debug)
                {
                    try
                    {
                        string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_CopyResults.log");
                        if (System.IO.File.Exists(logPath))
                        {
                            debugLog = System.IO.File.ReadAllText(logPath);
                        }
                    }
                    catch
                    {
                        // Reading the diagnostic must never fail the component.
                    }
                }
                dataAccess.SetData(index, debugLog);
            }
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