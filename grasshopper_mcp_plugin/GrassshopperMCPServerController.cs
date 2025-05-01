using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino;

namespace GrasshopperMCP
{
    class GrasshopperMCPServerController
    {
        private GH_Document doc;
        private GrasshopperMCPServer server;

        private ObservableCollection<string> logs;

        private bool isRunning = false;

        public GrasshopperMCPServerController(GH_Document doc, ObservableCollection<string> logs)
        {
            this.doc = doc;
            this.logs = logs;
        }


        public void StartServer()
        {
            if (isRunning)
            {
                logs.Add("Server is already running.");
                return;
            }

            if (server == null)
            {
                server = new GrasshopperMCPServer(doc, logs);
            }

            server.Start();
            isRunning = true;
            logs.Add("Server started.");
        }

        public void StopServer()
        {
            if (server != null)
            {
                server.Stop();
                server = null;
                isRunning = false;
                logs.Add("Server stopped.");
            }
        }

        public bool IsServerRunning()
        {
            return isRunning;
        }
    }
}
