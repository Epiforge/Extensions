namespace Epiforge.Extensions.Components;

/// <summary>
/// Invokes a constructor or a method through reflection without wrapping what it throws, which is how the invokers work where the runtime cannot generate code
/// </summary>
/// <remarks>
/// An argument of <see langword="null"/> for a parameter of a value type is passed as that type's default value here, where the generated invokers throw a <see cref="NullReferenceException"/>; an argument of the wrong type throws an <see cref="ArgumentException"/> here and an <see cref="InvalidCastException"/> there
/// </remarks>
sealed class ReflectedInvocation
{
    internal ReflectedInvocation(MethodBase member)
    {
#if IS_NET_8_0_OR_GREATER
        if (member is ConstructorInfo constructor)
            constructorInvoker = ConstructorInvoker.Create(constructor);
        else
            methodInvoker = MethodInvoker.Create(member);
#else
        this.member = member;
#endif
    }

#if IS_NET_8_0_OR_GREATER
    readonly ConstructorInvoker? constructorInvoker;
    readonly MethodInvoker? methodInvoker;
#else
    readonly MethodBase member;
#endif

    /// <summary>
    /// Finds the method of a generic interface the type implements over itself, such as <see cref="IComparable{T}.CompareTo(T)"/>, without constructing the interface type, which a runtime that cannot generate code may not be able to do
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "An interface the type implements over itself is one the default comparers use, so trimming keeps it; were it removed, the comparison falls back to the non-generic one, as the default comparers do for a type which does not implement it")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The method is read from an interface the type implements over itself, which trimming keeps for the reason above")]
    internal static ReflectedInvocation? OfOwnGenericInterface(Type type, Type genericInterfaceDefinition, string methodName)
    {
        foreach (var implemented in type.GetInterfaces())
            if (implemented.IsGenericType && implemented.GetGenericTypeDefinition() == genericInterfaceDefinition && implemented.GetGenericArguments()[0] == type && implemented.GetMethod(methodName) is { } method)
                return new(method);
        return null;
    }

    internal object Construct(object?[] arguments) =>
        Invoke(null, arguments)!;

    internal object? Invoke(object? instance, object?[] arguments) =>
#if IS_NET_8_0_OR_GREATER
        constructorInvoker is { } constructor ? constructor.Invoke(arguments) : methodInvoker!.Invoke(instance, arguments);
#else
        member is ConstructorInfo constructor ? constructor.Invoke(BindingFlags.DoNotWrapExceptions, null, arguments, null) : member.Invoke(instance, BindingFlags.DoNotWrapExceptions, null, arguments, null);
#endif

    internal object? InvokeWithNoArgument(object? instance) =>
#if IS_NET_8_0_OR_GREATER
        constructorInvoker is { } constructor ? constructor.Invoke() : methodInvoker!.Invoke(instance);
#else
        Invoke(instance, []);
#endif

    internal object? InvokeWithOneArgument(object? instance, object? argument0) =>
#if IS_NET_8_0_OR_GREATER
        constructorInvoker is { } constructor ? constructor.Invoke(argument0) : methodInvoker!.Invoke(instance, argument0);
#else
        Invoke(instance, [argument0]);
#endif

    internal object? InvokeWithTwoArguments(object? instance, object? argument0, object? argument1) =>
#if IS_NET_8_0_OR_GREATER
        constructorInvoker is { } constructor ? constructor.Invoke(argument0, argument1) : methodInvoker!.Invoke(instance, argument0, argument1);
#else
        Invoke(instance, [argument0, argument1]);
#endif
}
