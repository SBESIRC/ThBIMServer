using Xbim.Ifc;
using Xbim.Common.Geometry;
using Xbim.Ifc2x3.GeometricModelResource;

namespace ThBIMServer.Ifc2x3
{
    public static class ThProtoBuf2IFC2x3CSGSolidExtension
    {
        public static IfcBooleanResult ToIfcBooleanResult(this IfcStore model,
            IfcSolidModel first, IfcSolidModel second, IfcBooleanOperator op)
        {
            return model.Instances.New<IfcBooleanResult>(b =>
            {
                b.Operator = op;
                b.FirstOperand = first;
                b.SecondOperand = second;
            });
        }

        public static IfcCsgSolid ToIfcCsgSolid(this IfcStore model,
            IfcSolidModel first, IfcSolidModel second, IfcBooleanOperator op)
        {
            return model.Instances.New<IfcCsgSolid>(c =>
            {
                c.TreeRootExpression = model.ToIfcBooleanResult(first, second, op);
            });
        }

        public static IXbimSolid CreateXbimSolid(IfcCsgSolid csgSolid)
        {
            return ThXbimGeometryService.Instance.Engine.CreateSolid(csgSolid);
        }
    }
}
