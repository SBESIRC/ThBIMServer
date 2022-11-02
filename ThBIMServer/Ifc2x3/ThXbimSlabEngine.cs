using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Geometry.Engine.Interop;

namespace ThBIMServer.Ifc2x3
{
    public class ThXbimSlabEngine
    {
        public IfcStore Model { get; private set; }
        public IXbimGeometryEngine Engine { get; private set; }
        public ThXbimSlabEngine()
        {
            Engine = new XbimGeometryEngine();
            Model = ThIFC2x3Factory.CreateMemoryModel();
        }
    }
}
