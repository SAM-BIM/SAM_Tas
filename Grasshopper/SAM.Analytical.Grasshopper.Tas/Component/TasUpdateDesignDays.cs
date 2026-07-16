using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Tas.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Tas
{
    public class TasUpdateDesignDays : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("568df94a-ae3d-417d-b83e-b3d6af00bb1a");

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
        public TasUpdateDesignDays()
          : base("Tas.UpdateDesignDays", "Tas.UpdateDesignDays",
              "Updates the Design Days  in a TBD file.",
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_pathTasTBD", NickName = "_pathTasTBD", Description = "A string path to a TasTBD file", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooAnalyticalObjectParam() { Optional = true, Name = "_coolingDesignDays_", NickName = "_coolingDesignDays_", Description = "The SAM Analytical Design Days for Cooling", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooAnalyticalObjectParam() { Optional = true, Name = "_heatingDesignDays_", NickName = "_heatingDesignDays_", Description = "The SAM Analytical Design Days for Heating", Access = GH_ParamAccess.list }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Integer param_Integer = new global::Grasshopper.Kernel.Parameters.Param_Integer() { Name = "_repetitions_", NickName = "_repetitions_", Description = "Repetitions", Access = GH_ParamAccess.item };
                param_Integer.SetPersistentData(30);
                result.Add(new GH_SAMParam(param_Integer, ParamVisibility.Binding));

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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "guids", NickName = "guids", Description = "The GUIDS (Globally Unique Identifiers) of the Tas Design Days.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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

            index = Params.IndexOfInputParam("_coolingDesignDays_");
            List<DesignDay> coolingDesignDays = new List<DesignDay>();
            if (index != -1)
            {
                dataAccess.GetDataList(index, coolingDesignDays);
            }

            index = Params.IndexOfInputParam("_heatingDesignDays_");
            List<DesignDay> heatingDesignDays = new List<DesignDay>();
            if (index != -1)
            {
                dataAccess.GetDataList(index, heatingDesignDays);
            }

            index = Params.IndexOfInputParam("_repetitions_");
            int repetitions = 30;
            if (index != -1)
            {
                dataAccess.GetData(index, ref repetitions);
            }

            List<Guid> guids = null;
            if (coolingDesignDays != null || heatingDesignDays != null)
            {
                if(coolingDesignDays.Count != 0 || heatingDesignDays.Count != 0)
                {
                    guids = Analytical.Tas.Modify.AddDesignDays(path_TBD, coolingDesignDays, heatingDesignDays, repetitions);
                }
            }

            index = Params.IndexOfOutputParam("guids");
            if (index != -1)
            {
                dataAccess.SetData(index, guids?.ConvertAll(x => x.ToString()));
            }

            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, guids != null);
            }
        }
    }
}
