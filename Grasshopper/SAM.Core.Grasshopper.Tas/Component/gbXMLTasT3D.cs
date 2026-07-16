using Grasshopper.Kernel;
using SAM.Core.Grasshopper.Tas.Properties;
using System;
using System.Collections.Generic;

namespace SAM.Core.Grasshopper.Tas
{
    public class gbXMLTasT3D : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("ae9cae75-6909-421f-8e68-f541703347b3");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.3";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_T3D3;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public gbXMLTasT3D()
          : base("gbXML.TasT3D", "TasT3D",
              "Imports a gbXML file to a TasT3D file.",
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_pathgbXML", NickName = "_pathgbXML", Description = "The string path to a gbXML file.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean param_Boolean;

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_override_", NickName = "_override_", Description = "Do you want to override the import settings for the gbXML file?", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(true);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_fixNormals_", NickName = "_fixNormals_", Description = "Do you want to reverse the wrong normals using the Tas internal engine?", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_zonesFromSpaces_", NickName = "_zonesFromSpaces_", Description = "Do you want to transform the spaces for the internal Tas zones using the Tas internal engine?", Access = GH_ParamAccess.item };
                param_Boolean.SetPersistentData(true);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_useWidths_", NickName = "_useWidths_", Description = "Do you want to use the building element widths?", Access = GH_ParamAccess.item };
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

            index = Params.IndexOfInputParam("_pathgbXML");
            string path_gbXML = null;
            if (index == -1 || !dataAccess.GetData(index, ref path_gbXML) || string.IsNullOrWhiteSpace(path_gbXML))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_override_");
            bool @override = false;
            if (index == -1 || !dataAccess.GetData(index, ref @override))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_fixNormals_");
            bool fixNormals = true;
            if (index == -1 || !dataAccess.GetData(index, ref fixNormals))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_zonesFromSpaces_");
            bool zonesFromSpaces = true;
            if (index == -1 || !dataAccess.GetData(index, ref zonesFromSpaces))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_useWidths_");
            bool useWidths = true;
            if (index == -1 || !dataAccess.GetData(index, ref useWidths))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }


            bool result = Core.Tas.Convert.ToT3D(path_T3D, path_gbXML, @override, fixNormals, zonesFromSpaces, useWidths);

            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, result);
            }
        }
    }
}
