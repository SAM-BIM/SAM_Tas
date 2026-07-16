using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Tas.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Tas
{
    public class TasUpdateDesignLoads : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("b33d5f4a-785d-4acc-86d3-49034badb357");

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
        public TasUpdateDesignLoads()
          : base("Tas.UpdateDesignLoads", "Tas.UpdateDesignLoads",
              "Updates the design heating and cooling loads for spaces in an analytical model from a TBD zone maxCoolingLoad and maxHeatingLoad.",
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
                result.Add(new GH_SAMParam(new GooAnalyticalObjectParam() { Name = "_analyticalModel", NickName = "_analyticalModel", Description = "A SAM analytical model", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

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
                result.Add(new GH_SAMParam(new GooAnalyticalObjectParam() { Name = "analyticalModel", NickName = "analyticalModel", Description = "A SAM analytical model", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSpaceParam() { Name = "spaces", NickName = "spaces", Description = "SAM Analytcial Spaces", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooResultParam() { Name = "spaceSimulationResultsCooling", NickName = "spaceSimulationResultsCooling", Description = "Cooling SpaceSimulationResults", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooResultParam() { Name = "spaceSimulationResultsHeating", NickName = "spaceSimulationResultsHeating", Description = "Heating SpaceSimulationResults", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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

            index = Params.IndexOfInputParam("_analyticalModel");
            IAnalyticalObject analyticalObject = null;
            if (index == -1 || !dataAccess.GetData(index, ref analyticalObject) || analyticalObject == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<SpaceSimulationResult> spaceSimulationResults_Cooling = null;
            List<SpaceSimulationResult> spaceSimulationResults_Heating = null;
            List<Space> spaces = null;
            bool result = false;

            if (analyticalObject is AnalyticalModel)
            {
                analyticalObject = Analytical.Tas.Modify.UpdateDesignLoads(path_TBD, (AnalyticalModel)analyticalObject);
                AdjacencyCluster adjacencyCluster = ((AnalyticalModel)analyticalObject).AdjacencyCluster;
                if (adjacencyCluster != null)
                {
                    spaces = adjacencyCluster.GetSpaces();
                    if (spaces != null)
                    {
                        spaceSimulationResults_Cooling = new List<SpaceSimulationResult>();
                        spaceSimulationResults_Heating = new List<SpaceSimulationResult>();

                        foreach (Space space in spaces)
                        {
                            List<SpaceSimulationResult> spaceSimulationResults = adjacencyCluster.GetResults<SpaceSimulationResult>(space, Analytical.Tas.Query.Source());
                            if (spaceSimulationResults == null)
                            {
                                continue;
                            }

                            spaceSimulationResults_Cooling.AddRange(spaceSimulationResults.FindAll(x => x.LoadType() == LoadType.Cooling));
                            spaceSimulationResults_Cooling.AddRange(spaceSimulationResults.FindAll(x => x.LoadType() == LoadType.Heating));
                        }
                        result = true;
                    }

                }
            }
            else if(analyticalObject is BuildingModel)
            {
                BuildingModel buildingModel = new BuildingModel((BuildingModel)analyticalObject);
                spaces = Analytical.Tas.Modify.UpdateDesignLoads(buildingModel, path_TBD);
                if(spaces != null)
                {
                    spaceSimulationResults_Cooling = new List<SpaceSimulationResult>();
                    spaceSimulationResults_Heating = new List<SpaceSimulationResult>();

                    foreach (Space space in spaces)
                    {
                        List<SpaceSimulationResult> spaceSimulationResults = buildingModel.GetResults<SpaceSimulationResult>(space, Analytical.Tas.Query.Source());
                        if (spaceSimulationResults == null)
                        {
                            continue;
                        }

                        spaceSimulationResults_Cooling.AddRange(spaceSimulationResults.FindAll(x => x.LoadType() == LoadType.Cooling));
                        spaceSimulationResults_Cooling.AddRange(spaceSimulationResults.FindAll(x => x.LoadType() == LoadType.Heating));
                    }

                    result = true;
                }

                analyticalObject = buildingModel;
            }

            index = Params.IndexOfOutputParam("analyticalModel");
            if (index != -1)
            {
                dataAccess.SetData(index, new GooAnalyticalObject(analyticalObject));
            }

            index = Params.IndexOfOutputParam("spaces");
            if (index != -1)
            {
                dataAccess.SetDataList(index, spaces?.ConvertAll(x => new GooSpace(x)));
            }

            index = Params.IndexOfOutputParam("spaceSimulationResultsCooling");
            if (index != -1)
            {
                dataAccess.SetDataList(index, spaceSimulationResults_Cooling?.ConvertAll(x => new GooResult(x)));
            }

            index = Params.IndexOfOutputParam("spaceSimulationResultsHeating");
            if (index != -1)
            {
                dataAccess.SetDataList(index, spaceSimulationResults_Heating?.ConvertAll(x => new GooResult(x)));
            }

            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, result);
            }
        }
    }
}
