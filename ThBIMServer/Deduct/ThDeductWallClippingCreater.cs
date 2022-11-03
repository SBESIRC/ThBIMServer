using Xbim.Ifc;
using Xbim.Ifc2x3.SharedBldgElements;

namespace ThBIMServer.Deduct
{
    public static class ThDeductWallClippingCreater
    {
        public static void CreateClippingWall(IfcStore model, IfcWall archWall, IfcWall struWall)
        {
            using (var txn = model.BeginTransaction("Create Clipping Solid"))
            {
                var minuend = ThDeductFactory.ToIfcRepresentationItem(model, archWall);
                var subtractor = ThDeductFactory.ToIfcRepresentationItem(model, struWall, true);

                // 
                archWall.Representation = ThDeductFactory.CreateIfcBooleanClippingResult(model, minuend, subtractor);

                txn.Commit();
            }
        }
    }
}
