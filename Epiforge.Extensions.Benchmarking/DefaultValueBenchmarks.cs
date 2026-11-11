namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices a value type's default through <see cref="ReflectionExtensions.FastDefault(Type)"/> against the runtime's uninitialized object, which is that default with no constructor run, here and in the last release
/// </summary>
[AgainstReleasedExpressions("7.1.7")]
[MemoryDiagnoser]
public class DefaultValueBenchmarks
{
    readonly Type type = typeof(TimeSpan);

    [Benchmark(Baseline = true)]
    public object? FastDefault() =>
        type.FastDefault();

    [Benchmark]
    public object UninitializedObject() =>
        RuntimeHelpers.GetUninitializedObject(type);
}
