namespace Epiforge.Extensions.Blazor.Tests;

static class StaticHolder
{
    public static Person Person { get; } = new() { Name = "Static" };
}
