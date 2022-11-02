using NetTopologySuite.Geometries;
using NetTopologySuite.Mathematics;

namespace ThBIMServer.NTS
{
    public static class ThNTSOperation
    {
        public static Coordinate Addition(Coordinate location, Vector2D vector1, Vector2D vector2, double xDim, double yDim)
        {
            return new Coordinate(location.X + vector1.X * xDim + vector2.X * yDim, location.Y + vector1.Y * xDim + vector2.Y * yDim);
        }
    }
}
