using System;
using System.Collections.Generic;
using System.Linq;

using Xbim.Ifc;
using Xbim.Ifc2x3.GeometricConstraintResource;
using Xbim.Ifc2x3.GeometricModelResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProfileResource;
using Xbim.Ifc2x3.SharedBldgElements;

using ThBIMServer.NTS;

namespace ThBIMServer.Deduct
{
    public class ThDeductService
    {
        private const string inserted = "D:\\项目\\三维平台\\测试图\\ifc\\建筑结构合并.ifc";

        public void Deduct()
        {
            var archPath = "D:\\项目\\三维平台\\扣减\\output.ifc";
            var struPath = "D:\\项目\\三维平台\\测试图\\ifc\\0929-结构.ifc";

            // 解决方案1：
            //  第一步，解构：将结构IFC解构成中间模型
            //  第二步，和模：将建筑IFC和结构中间模型和模

            // 计算每个墙所在的楼层，并把墙添加到对应的楼层中
            // 取出建筑墙和剪力墙，获取他们的二维profile，并建立空间索引
            // 遍历剪力墙，在建筑墙的空间索引中查找被扣减的对象（具体的算法逻辑需要细化）
            // 对于需要扣减的对子（建筑墙和剪力墙)，建立开洞关系
            using (var model = IfcStore.Open(archPath))
            {
                var project = model.Instances.OfType<IfcProject>().FirstOrDefault();
                var struStoreys = project.Sites.First().Buildings.First().BuildingStoreys.ToList();
                foreach (var archStorey in project.Sites.Last().Buildings.First().BuildingStoreys.ToList())
                {
                    var struStorey = struStoreys.FirstOrDefault(o => StoreyCompare(o.Name.Value, archStorey.Name.Value));
                    if (struStorey == null)
                    {
                        continue;
                    }
                    var struWalls = new List<IfcWall>();
                    foreach (var r in struStorey.ContainsElements)
                    {
                        struWalls.AddRange(r.RelatedElements.OfType<IfcWall>());
                    }
                    var archWalls = new List<IfcWall>();
                    foreach (var r in archStorey.ContainsElements)
                    {
                        archWalls.AddRange(r.RelatedElements.OfType<IfcWall>());
                    }

                    var archProfileInfos = new List<Tuple<IfcProfileDef, IfcAxis2Placement>>();
                    archWalls.ForEach(o =>
                    {
                        var profile = ((IfcSweptAreaSolid)o.Representation.Representations[0].Items[0]).SweptArea;
                        var placement = ((IfcLocalPlacement)o.ObjectPlacement).RelativePlacement;
                        archProfileInfos.Add(Tuple.Create(profile, placement));
                    });

                    var spatialIndex = new ThIFCNTSSpatialIndex(archProfileInfos);
                    struWalls.ForEach(struWall =>
                    {
                        var solid = struWall.Representation.Representations[0].Items[0];
                        var profile = GetIfcProfileDef(solid);
                        var placement = ((IfcLocalPlacement)struWall.ObjectPlacement).RelativePlacement;
                        var filter = spatialIndex.SelectCrossingPolygon(Tuple.Create(profile, placement));
                        filter.ForEach(o =>
                        {
                            var crossWall = archWalls.Where(archWall => ((IfcSweptAreaSolid)archWall.Representation.Representations[0].Items[0]).SweptArea.Equals(o.Item1)).FirstOrDefault();
                            if (crossWall != null)
                            {
                                // 建立墙与墙之间的打洞关系
                                var ifcHole = ThDeductWallRelationCreater.CreateHole(model, struWall, (double)struStorey.Elevation.Value + 100);
                                ThDeductWallRelationCreater.BuildRelationship(model, crossWall, struWall, ifcHole);
                            }
                        });
                    });
                }

                model.SaveAs(inserted);
            }
        }

        private IfcProfileDef GetIfcProfileDef(IfcRepresentationItem solid)
        {
            if (solid is IfcSweptAreaSolid sweptArea)
            {
                return sweptArea.SweptArea;
            }
            else if (solid is IfcBooleanClippingResult clippingResult)
            {
                if (clippingResult.FirstOperand is IfcSweptAreaSolid e)
                {
                    return e.SweptArea;
                }
                else
                {
                    return GetIfcProfileDef(clippingResult.FirstOperand as IfcRepresentationItem);
                }
            }
            else
            {
                return null;
            }
        }

        private bool StoreyCompare(Xbim.Ifc4.MeasureResource.IfcLabel? str1, string str2)
        {
            return str1 == str2 || str1 == str2 + "F" || str1 + "F" == str2;
        }
    }
}
