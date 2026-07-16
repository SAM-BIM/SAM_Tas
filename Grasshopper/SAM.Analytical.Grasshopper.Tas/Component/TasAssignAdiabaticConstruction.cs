// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Tas.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Tas
{
    public class TasAssignAdiabaticConstruction : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("d1f6458f-bdd0-497b-82b5-fb454d4e5326");

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
        public TasAssignAdiabaticConstruction()
          : base("Tas.AssignAdiabaticConstruction", "Tas.AssignAdiabaticConstruction",
              "Assign an adiabatic construction to the building element in a TBD file.",
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_pathTasTBD", NickName = "_pathTasTBD", Description = "The string path to a TasTBD file.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_String param_String = new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_adiabaticConstructionName_", NickName = "_adiabaticConstructionName_", Description = "The assigned name of the adiabatic construction.", Access = GH_ParamAccess.item };
                param_String.SetPersistentData("Adiabatic");
                result.Add(new GH_SAMParam(param_String, ParamVisibility.Binding));

                param_String = new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_constructionNameSuffixes_", NickName = "_constructionNameSuffixes_", Description = "The suffixes with which the building element construction names will be replaced with.", Access = GH_ParamAccess.list };
                param_String.SetPersistentData(new string[] { "-unzoned", "-internal", "-exposed" });
                result.Add(new GH_SAMParam(param_String, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_caseSensitive_", NickName = "_caseSensitive_", Description = "Should we consider the capitalisation?", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_trim_", NickName = "_trim_", Description = "Do you want to trim the construction?", Access = GH_ParamAccess.item };
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "guids", NickName = "guids", Description = "The GUIDS (Globally Unique Identifiers) of the building elements construction that has been changed.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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

            index = Params.IndexOfInputParam("_adiabaticConstructionName_");
            string constructionName_Adiabatic = null;
            if (index == -1 || !dataAccess.GetData(index, ref constructionName_Adiabatic) || string.IsNullOrWhiteSpace(constructionName_Adiabatic))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_constructionNameSuffixes_");
            List<string> constructionNames_Sufixes = new List<string>();
            if (index == -1 || !dataAccess.GetDataList(index, constructionNames_Sufixes))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_caseSensitive_");
            bool caseSensitive = false;
            if (index == -1 || !dataAccess.GetData(index, ref caseSensitive))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_trim_");
            bool trim = false;
            if (index == -1 || !dataAccess.GetData(index, ref trim))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<Guid> result = Analytical.Tas.Modify.AssignAdiabaticConstruction(path_TBD, constructionName_Adiabatic, constructionNames_Sufixes, caseSensitive, trim);

            index = Params.IndexOfOutputParam("guids");
            if (index != -1)
            {
                dataAccess.SetData(index, result?.ConvertAll(x => x.ToString()));
            }

            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, result != null);
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
