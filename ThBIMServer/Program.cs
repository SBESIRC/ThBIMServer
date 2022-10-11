namespace ThBIMServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            PipeService service = new PipeService();
            service.Work();
        }
    }
}
