// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Core.Grasshopper.Tas.Properties;
using System;
using System.Collections.Generic;

namespace SAM.Core.Grasshopper.Tas
{
    public class TasCreateSurfaceOutputSpec : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("d928f41d-d39e-4fa7-811d-b42ba33b6c12");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.2";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_TasTBD3;

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public TasCreateSurfaceOutputSpec()
          : base("Tas.CreateSurfaceOutputSpec", "TasCreateSurfaceOutputSpec",
              "Creates SAM SurfaceOutputSpec \n * For Condensation you need also convection, temperature \n* For LongWave you need also solarGain, condensation, convection.",
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

                global::Grasshopper.Kernel.Parameters.Param_String param_String;

                param_String = new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_name_", NickName = "_name_", Description = "Name", Access = GH_ParamAccess.item, Optional = true };
                param_String.SetPersistentData("SAM SurfaceOutputSpec");
                result.Add(new GH_SAMParam(param_String, ParamVisibility.Binding));

                param_String = new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_description_", NickName = "_description_", Description = "Description", Access = GH_ParamAccess.item, Optional = true };
                result.Add(new GH_SAMParam(param_String, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean param_Boolean;

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_apertureData_", NickName = "_apertureData_", Description = "Aperture Data", Access = GH_ParamAccess.item, Optional = true };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_condensation_", NickName = "_condensation_", Description = "Condensation \n* you need also  convection, temperature", Access = GH_ParamAccess.item, Optional = true };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_convection_", NickName = "_convection_", Description = "Convection", Access = GH_ParamAccess.item, Optional = true };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_solarGain_", NickName = "_solarGain_", Description = "Solar Gain", Access = GH_ParamAccess.item, Optional = true };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_conduction_", NickName = "_conduction_", Description = "Conduction", Access = GH_ParamAccess.item, Optional = true };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_longWave_", NickName = "_longWave_", Description = "LongWave \n* you need also solarGain, condensation, convection  ", Access = GH_ParamAccess.item, Optional = true };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_temperature_", NickName = "_temperature_", Description = "Temperature", Access = GH_ParamAccess.item, Optional = true };
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
                result.Add(new GH_SAMParam(new GooSurfaceOutputSpecParam() { Name = "surfaceOutputSpec", NickName = "surfaceOutputSpec", Description = "SAM Core Tas SurfaceOutputSpec", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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

            index = Params.IndexOfInputParam("_name_");
            string name = null;
            if (index != -1)
            {
                dataAccess.GetData(index, ref name);
            }
            Core.Tas.SurfaceOutputSpec surfaceOutputSpec = new Core.Tas.SurfaceOutputSpec(name);

            index = Params.IndexOfInputParam("_description_");
            string description = null;
            if (index != -1)
            {
                dataAccess.GetData(index, ref description);
            }
            surfaceOutputSpec.Description = description;

            index = Params.IndexOfInputParam("_apertureData_");
            bool apertureData = false;
            if (index != -1)
            {
                dataAccess.GetData(index, ref apertureData);
            }
            surfaceOutputSpec.ApertureData = apertureData;

            index = Params.IndexOfInputParam("_condensation_");
            bool condensation = false;
            if (index != -1)
            {
                dataAccess.GetData(index, ref condensation);
            }
            surfaceOutputSpec.Condensation = condensation;

            index = Params.IndexOfInputParam("_convection_");
            bool convection = false;
            if (index != -1)
            {
                dataAccess.GetData(index, ref convection);
            }
            surfaceOutputSpec.Convection = convection;

            index = Params.IndexOfInputParam("_solarGain_");
            bool solarGain = false;
            if (index != -1)
            {
                dataAccess.GetData(index, ref solarGain);
            }
            surfaceOutputSpec.SolarGain = solarGain;

            index = Params.IndexOfInputParam("_conduction_");
            bool conduction = false;
            if (index != -1)
            {
                dataAccess.GetData(index, ref conduction);
            }
            surfaceOutputSpec.Conduction = conduction;

            index = Params.IndexOfInputParam("_longWave_");
            bool longWave = false;
            if (index != -1)
            {
                dataAccess.GetData(index, ref longWave);
            }
            surfaceOutputSpec.LongWave = longWave;

            index = Params.IndexOfInputParam("_temperature_");
            bool temperature = false;
            if (index != -1)
            {
                dataAccess.GetData(index, ref temperature);
            }
            surfaceOutputSpec.Temperature = temperature;


            index = Params.IndexOfOutputParam("surfaceOutputSpec");
            if (index != -1)
            {
                dataAccess.SetData(index, surfaceOutputSpec);
            }
        }
    }
}
