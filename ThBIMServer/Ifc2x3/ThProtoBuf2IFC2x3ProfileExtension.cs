using Xbim.Ifc;
using Xbim.Common.Geometry;
using Xbim.Ifc2x3.ProfileResource;

namespace ThBIMServer.Ifc2x3
{
    public static class ThProtoBuf2IFC2x3ProfileExtension
    {
        public static IfcRectangleProfileDef ToIfcRectangleProfileDef(this IfcStore model, double xDim, double yDim)
        {
            return model.Instances.New<IfcRectangleProfileDef>(d =>
            {
                d.XDim = xDim;
                d.YDim = yDim;
                d.ProfileType = IfcProfileTypeEnum.AREA;
                d.Position = model.ToIfcAxis2Placement2D(XbimPoint3D.Zero);
            });
        }

        public static IfcArbitraryProfileDefWithVoids ToIfcArbitraryProfileDefWithVoids(this IfcStore model, ThTCHMPolygon e)
        {
            return model.Instances.New<IfcArbitraryProfileDefWithVoids>(d =>
            {
                d.ProfileType = IfcProfileTypeEnum.AREA;
                d.OuterCurve = model.ToIfcCompositeCurve(e.Shell);
                foreach (var hole in e.Holes)
                {
                    d.InnerCurves.Add(model.ToIfcCompositeCurve(hole));
                }
            });
        }

        public static IfcArbitraryClosedProfileDef ToIfcArbitraryClosedProfileDef(this IfcStore model, ThTCHMPolygon e)
        {
            return model.Instances.New<IfcArbitraryClosedProfileDef>(d =>
            {
                d.ProfileType = IfcProfileTypeEnum.AREA;
                d.OuterCurve = model.ToIfcCompositeCurve(e.Shell);
            });
        }

        public static IfcArbitraryClosedProfileDef ToIfcArbitraryClosedProfileDef(this IfcStore model, ThTCHPolyline e)
        {
            return model.Instances.New<IfcArbitraryClosedProfileDef>(d =>
            {
                d.ProfileType = IfcProfileTypeEnum.AREA;
                d.OuterCurve = model.ToIfcCompositeCurve(e);
            });
        }
    }
}
