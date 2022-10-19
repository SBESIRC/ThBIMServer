using Xbim.Ifc;
using Xbim.Ifc2x3.Kernel;
using System.Collections.Generic;
using Xbim.Ifc2x3.ProductExtension;

namespace ThBIMServer.Ifc2x3
{
    public static class ThProtoBuf2IFC2x3RelAggregatesFactory
    {
        public static void Create(IfcStore model, IfcBuilding building, List<IfcBuildingStorey> storeys)
        {
            using (var txn = model.BeginTransaction())
            {
                var ifcRel = model.Instances.New<IfcRelAggregates>();
                ifcRel.RelatingObject = building;
                storeys.ForEach(s => ifcRel.RelatedObjects.Add(s));
                txn.Commit();
            }
        }
    }
}
