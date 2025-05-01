using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GrasshopperMCP
{
    public class GrasshopperMCPComponent : GH_Component
    {
        private ObservableCollection<string> logs = new ObservableCollection<string>();
        private GrasshopperMCPServerController serverController;
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public GrasshopperMCPComponent()
            : base("GrasshopperMCP", "GrasshopperMCP",
                "MCP server allows Grasshopper to communicate with AI agents",
                "Params", "Util")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Reset", "R", "Reset the MCP server", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Logs", "L", "Logs", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool reset = false;
            if (!DA.GetData(0, ref reset)) return;

            if (serverController == null)
            {
                var doc = Instances.ActiveCanvas.Document;
                serverController = new GrasshopperMCPServerController(doc,logs);
            }

            if (!serverController.IsServerRunning())
            {
                serverController.StartServer();
            }

            // if (serverController == null)
            // {
            //     
            //     logs.CollectionChanged += (sender, e) =>
            //     {
            //         DA.SetDataList(0, logs);
            //     };
            // }

            DA.SetDataList(0, logs);
            serverController.StartServer();
        }

        /// <summary>
        /// The Exposure property controls where in the panel a component icon 
        /// will appear. There are seven possible locations (primary to septenary), 
        /// each of which can be combined with the GH_Exposure.obscure flag, which 
        /// ensures the component will only be visible on panel dropdowns.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;



        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("764b2591-e8b4-4156-ac41-6184370b586c");
    }
}