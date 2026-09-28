namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Runs a class against a released version of Epiforge.Extensions.Expressions from NuGet as well as against the code in this repository, so that what the code costs against that release is measured in one run rather than by comparing runs
/// </summary>
/// <remarks>
/// The released version's job builds this project with <c>ReleasedExpressions</c> set, which swaps the project reference for a package reference; Collections and Components remain this repository's in both jobs. Every benchmark in this project must therefore compile against the released version's public surface
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
sealed class AgainstReleasedExpressionsAttribute(string version) :
    Attribute,
    IConfigSource
{
    public IConfig Config { get; } = ManualConfig.CreateEmpty().AddJob(Program.CurrentJob.WithArguments([new MsBuildArgument($"/p:ReleasedExpressions={version}")]).WithId(version));
}
