using Xbim.Ifc;
using System.Collections.Generic;
using Xbim.Ifc2x3.SharedBldgElements;

namespace ThBIMServer.Ifc2x3
{
    public class ThProtoBuf2IFC2x3RelDefinesFactory
    {
        public static void RelDefinesByType2Wall(IfcStore model, List<IfcWall> walls, IfcWallTypeEnum wallType)
        {
            using (var txn = model.BeginTransaction())
            {
                var type = model.Instances.New<IfcWallType>(t =>
                {
                    t.PredefinedType = wallType;
                });
                walls.ForEach(w => w.AddDefiningType(type));
                txn.Commit();
            }
        }
    }
}
