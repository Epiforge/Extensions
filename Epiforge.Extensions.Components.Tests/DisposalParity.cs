namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class DisposalParity
{
    static List<string> DeclaredSurfaceOf(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var surface = new List<string>();
        foreach (var member in type.GetMembers(flags))
            switch (member)
            {
                case ConstructorInfo constructor when !constructor.IsPrivate:
                    surface.Add($"constructor({ParametersOf(constructor)})");
                    break;
                case EventInfo eventInfo:
                    surface.Add($"event {eventInfo.EventHandlerType!.Name} {eventInfo.Name}");
                    break;
                case PropertyInfo property:
                    surface.Add($"property {property.PropertyType.Name} {property.Name}{((property.GetMethod?.IsPublic ?? false) ? " get" : string.Empty)}{((property.SetMethod?.IsPublic ?? false) ? " set" : string.Empty)}");
                    break;
                case MethodInfo method when !method.IsSpecialName && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly):
                    surface.Add($"method {method.ReturnType.Name} {method.Name}({ParametersOf(method)}){(method.IsAbstract ? " abstract" : method.IsVirtual ? " virtual" : string.Empty)}");
                    break;
            }
        surface.Sort(StringComparer.Ordinal);
        return surface;
    }

    [TestMethod]
    public void EachDynamicVariantDeclaresWhatItsCounterpartDeclares()
    {
        var pairs = new (Type counterpart, Type dynamicVariant)[]
        {
            (typeof(Components.AsyncDisposable), typeof(Components.DynamicAsyncDisposable)),
            (typeof(Components.Disposable), typeof(Components.DynamicDisposable)),
            (typeof(Components.SyncDisposable), typeof(Components.DynamicSyncDisposable))
        };
        foreach (var (counterpart, dynamicVariant) in pairs)
            CollectionAssert.AreEqual(DeclaredSurfaceOf(counterpart), DeclaredSurfaceOf(dynamicVariant), $"{counterpart.Name} and {dynamicVariant.Name} no longer declare the same members; these are maintained as copies of one another and a change to either belongs in both");
    }

    [TestMethod]
    public void EachPlainVariantDeclaresWhatItsNotifyingCounterpartDeclares()
    {
        const string renamedSynchronousHook = "method Boolean Dispose(Boolean) abstract";
        const string requiredAsynchronousHook = "method ValueTask`1 DisposeAsyncCore() virtual";
        var pairs = new (Type notifying, Type plain, (string from, string to)[] divergences)[]
        {
            (typeof(Components.AsyncDisposable), typeof(Components.PlainAsyncDisposable), []),
            (typeof(Components.Disposable), typeof(Components.PlainDisposable), [(renamedSynchronousHook, "method Boolean DisposeCore() abstract"), (requiredAsynchronousHook, "method ValueTask`1 DisposeAsyncCore() abstract")]),
            (typeof(Components.SyncDisposable), typeof(Components.PlainSyncDisposable), [(renamedSynchronousHook, "method Boolean DisposeCore() abstract")])
        };
        foreach (var (notifying, plain, divergences) in pairs)
        {
            var expected = DeclaredSurfaceOf(notifying);
            expected.RemoveAll(member => member.StartsWith("method Void Finalize(", StringComparison.Ordinal) || member.StartsWith("method Void LoggerSet(", StringComparison.Ordinal));
            foreach (var (from, to) in divergences)
            {
                var index = expected.IndexOf(from);
                Assert.IsTrue(index >= 0, $"{notifying.Name} no longer declares {from}, so the divergence recorded here for {plain.Name} is stale");
                expected[index] = to;
            }
            expected.Sort(StringComparer.Ordinal);
            CollectionAssert.AreEqual(expected, DeclaredSurfaceOf(plain), $"{notifying.Name} and {plain.Name} differ by more than the finalizer, the logger hook, and the divergences recorded here; a change to either usually belongs in both");
        }
    }

    static string ParametersOf(MethodBase method) =>
        string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name));
}
