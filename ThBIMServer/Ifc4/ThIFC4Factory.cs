using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Ifc;
using Xbim.Ifc4.GeometryResource;

namespace ThBIMServer.Ifc4
{
    public static class ThIFC4Factory
    {
        public static IfcCompositeCurve CreateIfcCompositeCurve(IfcStore model)
        {
            return model.Instances.New<IfcCompositeCurve>();
        }

        public static IfcCompositeCurveSegment CreateIfcCompositeCurveSegment(IfcStore model)
        {
            return model.Instances.New<IfcCompositeCurveSegment>(s =>
            {
                s.SameSense = true;
            });
        }
    }
}
