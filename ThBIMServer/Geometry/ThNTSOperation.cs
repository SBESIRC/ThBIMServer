using NetTopologySuite.Operation.Buffer;

namespace ThBIMServer.Geometry
{
    public static class ThNTSOperation
    {
        public static ThTCHPolyline BufferPL(this ThTCHPolyline polyline, double distance)
        {
            var buffer = new BufferOp(polyline.ToNTSLineString(), new BufferParameters()
            {
                JoinStyle = JoinStyle.Mitre,
                EndCapStyle = EndCapStyle.Square,
            });
            return buffer.GetResultGeometry(distance).ToTCHPolyline();
        }

        public static ThTCHPolyline BufferFlatPL(this ThTCHPolyline polyline, double distance)
        {
            var buffer = new BufferOp(polyline.ToNTSLineString(), new BufferParameters()
            {
                JoinStyle = JoinStyle.Mitre,
                EndCapStyle = EndCapStyle.Flat,
            });
            return buffer.GetResultGeometry(distance).ToTCHPolyline();
        }
    }
}
