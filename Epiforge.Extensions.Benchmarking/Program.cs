namespace Epiforge.Extensions.Benchmarking;

static class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--footprint")
        {
            QueryFootprintReport.Run();
            return;
        }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
