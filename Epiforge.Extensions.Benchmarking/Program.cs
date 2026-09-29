namespace Epiforge.Extensions.Benchmarking;

static class Program
{
    /// <summary>
    /// Three launches of ten 250 ms iterations each, after four warmup iterations, so that each figure carries the variance between processes as well as the variance within one
    /// </summary>
    internal static readonly Job CurrentJob = Job.Default.WithLaunchCount(3).WithWarmupCount(4).WithIterationCount(10).WithIterationTime(TimeInterval.FromMilliseconds(250)).WithId("current");

    static readonly IConfig configuration = DefaultConfig.Instance.AddJob(CurrentJob);

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
        if (args.Length > 0 && args[0] == "--soak")
        {
            await KeyedViewSoakReport.RunAsync(args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 20000).ConfigureAwait(false);
            return;
        }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, configuration);
    }
}
