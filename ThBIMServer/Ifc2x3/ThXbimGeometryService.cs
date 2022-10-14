using System;
using Xbim.Geometry.Engine.Interop;

namespace ThBIMServer.Ifc2x3
{
    public sealed class ThXbimGeometryService
    {
        private static readonly Lazy<ThXbimGeometryService> lazy =
            new Lazy<ThXbimGeometryService>(() => new ThXbimGeometryService());

        public static ThXbimGeometryService Instance { get { return lazy.Value; } }

        public readonly XbimGeometryEngine Engine;

        private ThXbimGeometryService()
        {
            Engine = new XbimGeometryEngine();
        }
    }
}
