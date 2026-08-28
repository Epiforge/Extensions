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

    static string ParametersOf(MethodBase method) =>
        string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name));
}
