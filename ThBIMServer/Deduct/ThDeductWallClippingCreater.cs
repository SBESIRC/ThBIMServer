using System;
using System.Linq;

using Xbim.Ifc;
using Xbim.Ifc2x3.Kernel;
using Xbim.Common.Geometry;
using Xbim.Ifc2x3.UtilityResource;
using Xbim.Ifc2x3.MeasureResource;
using Xbim.Ifc2x3.ProfileResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.PropertyResource;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Ifc2x3.GeometricModelResource;
using Xbim.Ifc2x3.RepresentationResource;
using Xbim.Ifc2x3.GeometricConstraintResource;

using ThBIMServer.Ifc2x3;

namespace ThBIMServer.Deduct
{
    public static class ThDeductWallClippingCreater
    {
        public static void CreateClippingWall(IfcStore model, IfcWall archWall, IfcWall struWall)
        {
            using (var txn = model.BeginTransaction("Create Clipping Solid"))
            {
                var minuend = ThDeductFactory.ToIfcRepresentationItem(model, archWall);
                var subtractor = ThDeductFactory.ToIfcRepresentationItem(model, struWall);

                // 
                archWall.Representation = ThDeductFactory.CreateIfcBooleanClippingResult(model, minuend, subtractor);

                txn.Commit();
            }
        }
    }
}
