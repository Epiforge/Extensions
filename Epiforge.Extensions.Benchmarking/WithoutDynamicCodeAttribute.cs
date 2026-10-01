namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Also runs a class with dynamic code turned off, which is the state of an application compiled ahead of time, so that what the libraries cost there is measured beside what they cost with a JIT in the same run
/// </summary>
/// <remarks>
/// The job builds this project with <c>DynamicCodeSupport</c> false, so <see cref="RuntimeFeature.IsDynamicCodeSupported"/> is false in its process and the libraries take the paths they take under Native AOT or on iOS, while the code itself still runs on the JIT; it measures the paths, not an ahead-of-time compiler. It runs only against the code in this repository, because a release before the libraries learned to run without dynamic code throws there
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
sealed class WithoutDynamicCodeAttribute :
    Attribute,
    IConfigSource
{
    const string id = "no dynamic code";

    public IConfig Config { get; } = ManualConfig.CreateEmpty()
        .AddJob(Program.CurrentJob.WithArguments([new MsBuildArgument("/p:DynamicCodeSupport=false")]).WithId(id));
}
