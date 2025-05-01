using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace GrasshopperMCP
{
    public class GrasshopperMCPInfo : GH_AssemblyInfo
    {
        public override string Name => "GrasshopperMCP";

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "GrasshopperMCP allows Grasshopper to communicate with AI agents";

        public override Guid Id => new Guid("aff0e703-dbab-411c-a16c-51b5a4555327");

        //Return a string identifying you or your company.
        public override string AuthorName => "Jingcheng Chen";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";

        //Return a string representing the version.  This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}