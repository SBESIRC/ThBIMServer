using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThMEPIFC.Ifc2x3;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc2x3.SharedBldgElements;

namespace ThBIMServer
{
    public class IfcDeductService
    {
        private const string inserted = "D:\\项目\\三维平台\\测试图\\ifc\\建筑结构合并.ifc";

        public void Deduct()
        {
            var archPath = "D:\\项目\\三维平台\\测试图\\ifc\\建筑1012.ifc";
            var struPath = "D:\\项目\\三维平台\\测试图\\ifc\\TH000000_标准测试项目_1#楼_结构_IFC模型.ifc";

            using (var archModel = IfcStore.Open(archPath))
            using (var struModel = IfcStore.Open(struPath))
            {
                var shearWalls = struModel.Instances.OfType<IfcWall>().ToList();
                //var wall = ThProtoBuf2IFC2x3Factory.CreateWall(model, thtchwall, floor_origin);

                ThProtoBuf2IFC2x3Builder.MergeShearWalls(archModel, shearWalls);
                archModel.SaveAs(inserted);
            }

        }
    }
}
