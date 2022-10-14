using System;
using Xbim.Geometry.Engine.Interop;

namespace ThBIMServer.Ifc2x3
{
    public sealed class ThXbimGeometryService
    {
        //==============SINGLETON============
        //fourth version from:
        //http://csharpindepth.com/Articles/General/Singleton.aspx
        private static readonly Lazy<ThXbimGeometryService> lazy =
            new Lazy<ThXbimGeometryService>(() => new ThXbimGeometryService());
        public static ThXbimGeometryService Instance { get { return lazy.Value; } }
        //-------------SINGLETON-----------------

        public readonly XbimGeometryEngine Engine;

        private ThXbimGeometryService()
        {
            Engine = new XbimGeometryEngine();
        }
    }
}
