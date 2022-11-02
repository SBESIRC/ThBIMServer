﻿using System.Linq;

using Xbim.Ifc;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc2x3.Kernel;

using ThBIMServer.Ifc2x3;
using ThBIMServer.Ifc4;
using ThBIMServer.ModelMerge;

namespace ThBIMServer.Deduct
{
    public class IfcDeductService
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
            //using (var iModel = IfcStore.Open(archPath))
            //{
            //    using (var model = IfcStore.Open(struPath))
            //    {
            //        var project2x3 = model.Instances.OfType<Xbim.Ifc2x3.Kernel.IfcProject>().FirstOrDefault();
            //        if (project2x3 != null)
            //        {
            //            var mergeService = new ThModelMergeService();
            //            mergeService.ModelMerge2x3(iModel, project2x3);
            //        }
            //        else
            //        {
            //            var project4 = model.Instances.OfType<Xbim.Ifc4.Kernel.IfcProject>().FirstOrDefault();
            //            if (project4 != null)
            //            {
            //                var mergeService = new ThModelMergeService();
            //                mergeService.ModelMerge4(iModel, project4);
            //            }
            //        }
            //    }

            //    var creater = new IfcWallRelationCreater();
            //    creater.CreateRelation(iModel);

            //    iModel.SaveAs(inserted);
            //}

            using (var iModel = IfcStore.Open(archPath))
            {
                var creater = new IfcDeductWallRelationCreater();
                creater.CreateRelationInSites(iModel);

                iModel.SaveAs(inserted);
            }
        }

        private PropertyTranformDelegate Filter()
        {
            PropertyTranformDelegate semanticFilter = (property, parentObject) =>
            {
                ////leave out geometry and placement
                //if (parentObject is IfcProduct &&
                //    (property.PropertyInfo.Name == nameof(IfcProduct.Representation) ||
                //    property.PropertyInfo.Name == nameof(IfcProduct.ObjectPlacement)))
                //    return null;

                ////leave out mapped geometry
                //if (parentObject is IfcTypeProduct &&
                //     property.PropertyInfo.Name == nameof(IfcTypeProduct.RepresentationMaps))
                //    return null;

                ////only bring over IsDefinedBy and IsTypedBy inverse relationships which will take over all properties and types
                //if (property.EntityAttribute.Order < 0 && !(
                //    property.PropertyInfo.Name == nameof(IfcProduct.IsDefinedBy) ||
                //    property.PropertyInfo.Name == nameof(IfcProduct.IsTypedBy)))
                //    return null;

                return property.PropertyInfo.GetValue(parentObject, null);
            };
            return semanticFilter;
        }
    }
}
