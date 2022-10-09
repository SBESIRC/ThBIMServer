namespace ThBIMServer
{
    class Program
    {
        static void Main(string[] args)
        {
            PipeService service = new PipeService();
            service.Work();
        }
    }
}
