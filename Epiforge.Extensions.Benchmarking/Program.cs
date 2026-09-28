namespace Epiforge.Extensions.Benchmarking;

static class Program
{
    /// <summary>
    /// Three launches of ten 250 ms iterations each, after four warmup iterations, so that each figure carries the variance between processes as well as the variance within one
    /// </summary>
    static readonly IConfig configuration = DefaultConfig.Instance.AddJob(Job.Default.WithLaunchCount(3).WithWarmupCount(4).WithIterationCount(10).WithIterationTime(TimeInterval.FromMilliseconds(250)));

    static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--footprint")
        {
            QueryFootprintReport.Run();
            return;
        }
        if (args.Length > 0 && args[0] == "--stress")
        {
            await ConcurrencyStressReport.RunAsync(args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 200000).ConfigureAwait(false);
            return;
        }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, configuration);
    }
}
