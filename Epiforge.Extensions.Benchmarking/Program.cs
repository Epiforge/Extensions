namespace Epiforge.Extensions.Benchmarking;

static class Program
{
    /// <summary>
    /// Three launches of ten 250 ms iterations each, after four warmup iterations, so that each figure carries the variance between processes as well as the variance within one
    /// </summary>
    static readonly IConfig configuration = DefaultConfig.Instance.AddJob(Job.Default.WithLaunchCount(3).WithWarmupCount(4).WithIterationCount(10).WithIterationTime(TimeInterval.FromMilliseconds(250)));

    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--footprint")
        {
            QueryFootprintReport.Run();
            return;
        }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, configuration);
    }
}
