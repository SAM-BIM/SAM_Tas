// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SAM.Analytical.Grasshopper.Tas.Properties;
using SAM.Analytical.Tas;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Tas.Obsolete
{
    [Obsolete("Obsolete since 2021-01-27")]
    public class TasTSDCreateAdjacencyCluster : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("a219f03b-1990-4b81-9c66-74fccd3ff62a");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.2";

        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_TasTSD3;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public TasTSDCreateAdjacencyCluster()
          : base("Tas.TSDCreateAdjacencyCluster", "Tas.TSDCreateAdjacencyCluster",
              "Creates an adjacency cluster from a TasTSD file.",
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

                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_pathTasTSD", NickName = "_pathTasTSD", Description = "A string path to a TasTSD file.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_GenericObject() { Name = "panelDataType_", NickName = "panelDataType_", Description = "Filters your chosen results for the type: panel.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_GenericObject() { Name = "spaceDataType_", NickName = "spaceDataType_", Description = "Filters your chosen results for the type: space.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_run", NickName = "_run", Description = "Connect a boolean toggle to run.", Access = GH_ParamAccess.item };
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

                result.Add(new GH_SAMParam(new GooAdjacencyClusterParam() { Name = "adjacencyCluster", NickName = "adjacencyCluster", Description = "A SAM analytical adjacency cluster", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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
            int index_Successful = Params.IndexOfOutputParam("successful");
            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, false);
            }

            int index = -1;

            bool run = false;
            index = Params.IndexOfInputParam("_run");
            if (index == -1 || !dataAccess.GetData(index, ref run))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }
            if (!run)
                return;

            string path_TSD = null;
            index = Params.IndexOfInputParam("_pathTasTSD");
            if (index == -1 || !dataAccess.GetData(index, ref path_TSD) || string.IsNullOrWhiteSpace(path_TSD))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<GH_ObjectWrapper> objectWrappers;

            List<PanelDataType> panelDataTypes = null;

            objectWrappers = new List<GH_ObjectWrapper>();
            index = Params.IndexOfInputParam("panelDataType_");
            if (index != -1 && dataAccess.GetDataList(index, objectWrappers))
            {
                panelDataTypes = new List<PanelDataType>();
                foreach(GH_ObjectWrapper objectWrapper in objectWrappers)
                {
                    PanelDataType panelDataType = PanelDataType.Undefined;
                    if (objectWrapper.Value is GH_String)
                        panelDataType = Analytical.Tas.Query.PanelDataType(((GH_String)objectWrapper.Value).Value);
                    else
                        panelDataType = Analytical.Tas.Query.PanelDataType(objectWrapper.Value);

                    if (panelDataType != PanelDataType.Undefined)
                        panelDataTypes.Add(panelDataType);
                }
            }

            List<SpaceDataType> spaceDataTypes = null;

            objectWrappers = new List<GH_ObjectWrapper>();
            index = Params.IndexOfInputParam("spaceDataType_");
            if (index != -1 && dataAccess.GetDataList(index, objectWrappers))
            {
                spaceDataTypes = new List<SpaceDataType>();
                foreach (GH_ObjectWrapper objectWrapper in objectWrappers)
                {
                    SpaceDataType spaceDataType = SpaceDataType.Undefined;
                    if (objectWrapper.Value is GH_String)
                        spaceDataType = Analytical.Tas.Query.SpaceDataType(((GH_String)objectWrapper.Value).Value);
                    else
                        spaceDataType = Analytical.Tas.Query.SpaceDataType(objectWrapper.Value);

                    if (spaceDataType != SpaceDataType.Undefined)
                        spaceDataTypes.Add(spaceDataType);
                }
            }

            AdjacencyCluster adjacencyCluster = path_TSD.ToSAM_AdjacencyCluster(spaceDataTypes, panelDataTypes);

            index = Params.IndexOfOutputParam("adjacencyCluster");
            if (index != -1)
            {
                dataAccess.SetData(index, adjacencyCluster);
            }
            if (index_Successful != -1)
            {
                dataAccess.SetData(index_Successful, adjacencyCluster != null);
            }
        }
    }
}
