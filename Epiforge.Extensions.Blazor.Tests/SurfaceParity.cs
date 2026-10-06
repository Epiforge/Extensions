namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class SurfaceParity
{
    static List<string> SurfaceOf(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var surface = new List<string>();
        foreach (var member in type.GetMembers(flags))
            switch (member)
            {
                case PropertyInfo property when (property.GetMethod?.IsPublic ?? false) || (property.GetMethod?.IsFamily ?? false):
                    surface.Add($"property {property.PropertyType.Name} {property.Name}{(property.GetMethod!.IsPublic ? " public" : " protected")}{(property.SetMethod is { } setter && (setter.IsPublic || setter.IsFamily) ? " set" : string.Empty)}");
                    break;
                case MethodInfo method when !method.IsSpecialName && (method.IsPublic || method.IsFamily) && method.Name != "Finalize":
                    surface.Add($"method {method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))}){(method.IsPublic ? " public" : " protected")}{(method.IsVirtual && !method.IsFinal ? " virtual" : string.Empty)}");
                    break;
            }
        surface.Sort(StringComparer.Ordinal);
        return surface;
    }

    static void AssertDeclaresEverything(Type counterpart, Type type, params string[] overridden)
    {
        var declared = SurfaceOf(type);
        var missing = SurfaceOf(counterpart).Where(member => !declared.Contains(member) && !overridden.Contains(member)).ToList();
        Assert.AreEqual(0, missing.Count, $"{type.Name} does not declare what {counterpart.Name} declares:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
        foreach (var interfaceType in counterpart.GetInterfaces())
            Assert.IsTrue(interfaceType.IsAssignableFrom(type), $"{type.Name} does not implement {interfaceType.Name}");
    }

    [TestMethod]
    public void ReactiveComponentBaseDeclaresWhatComponentBaseDeclares() =>
        AssertDeclaresEverything(typeof(ComponentBase), typeof(ReactiveComponentBase));

    [TestMethod]
    public void ReactiveLayoutComponentBaseDeclaresWhatLayoutComponentBaseDeclares() =>
        AssertDeclaresEverything(typeof(LayoutComponentBase), typeof(ReactiveLayoutComponentBase));
}
