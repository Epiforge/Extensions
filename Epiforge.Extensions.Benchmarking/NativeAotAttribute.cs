namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Also runs a class compiled ahead of time by Native AOT, so that the paths the libraries take without dynamic code are measured where an application actually takes them, beside the JIT with dynamic code turned off
/// </summary>
/// <remarks>
/// Publishing for Native AOT on Windows needs the MSVC build tools and a Windows SDK, which Visual Studio's Desktop development with C++ workload installs. It runs only against the code in this repository, for the reason <see cref="WithoutDynamicCodeAttribute"/> gives
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
sealed class NativeAotAttribute :
    Attribute,
    IConfigSource
{
    const string id = "native aot";

    public IConfig Config { get; } = ManualConfig.CreateEmpty()
        .AddJob(Program.CurrentJob.WithToolchain(NativeAotToolchain.Net10_0).WithId(id));
}
