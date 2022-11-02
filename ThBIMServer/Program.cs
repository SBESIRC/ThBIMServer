using ThBIMServer.Deduct;

namespace ThBIMServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //var service = new PipeService();
            //service.Work();

            var service = new ThDeductService();
            service.Deduct();
        }
    }
}
