namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Runs a class against a released version of Epiforge.Extensions.Expressions from NuGet as well as against the code in this repository, so that what the code costs against that release is measured in one run rather than by comparing runs
/// </summary>
/// <remarks>
/// The released version's job builds this project with <c>ReleasedExpressions</c> set, which swaps the project references for a package reference, so that job runs the released Expressions with the Collections and Components it was released against. Every benchmark in this project must therefore compile against the public surface of those three releases
/// </remarks>
/// <remarks>
/// That job runs only this library's arms and the floors they stand on. An arm measuring DynamicData, NMF Expressions or ObservableComputations runs the same code in both jobs, and an arm which changes a view and then reads it exists to catch a view which defers its work, which a comparison with this library's own release does not ask, so the released job leaves both to the current one
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
sealed class AgainstReleasedExpressionsAttribute(string version) :
    Attribute,
    IConfigSource
{
    public IConfig Config { get; } = ManualConfig.CreateEmpty()
        .AddJob(Program.CurrentJob.WithArguments([new MsBuildArgument($"/p:ReleasedExpressions={version}")]).WithId(version))
        .AddFilter(new SimpleFilter(benchmarkCase => benchmarkCase.Job.Id != version || !ComparesAnotherLibraryOrReads(benchmarkCase.Descriptor.WorkloadMethod.Name)));

    static bool ComparesAnotherLibraryOrReads(string armName) =>
        armName.Contains("DynamicData", StringComparison.Ordinal) || armName.Contains("Nmf", StringComparison.Ordinal) || armName.Contains("ObservableComputations", StringComparison.Ordinal) || armName.Contains("ThenRead", StringComparison.Ordinal);
}
