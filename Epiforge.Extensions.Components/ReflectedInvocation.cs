namespace Epiforge.Extensions.Components;

/// <summary>
/// Invokes a constructor or a method through reflection without wrapping what it throws, which is how the invokers work where the runtime cannot generate code
/// </summary>
/// <remarks>
/// The instance and the arguments are cast first as the generated invokers cast them, so that what one refuses the other refuses with the same exception, and what one converts, such as an enumeration value for a parameter of its underlying type, the other converts; an instance method refuses a <see langword="null"/> instance with a <see cref="NullReferenceException"/>, as calling it does
/// </remarks>
sealed class ReflectedInvocation
{
    internal ReflectedInvocation(MethodBase member)
    {
        if (member is MethodInfo { IsStatic: false, DeclaringType: { } declaringType })
            instanceCast = new(declaringType);
        var parameters = member.GetParameters();
        argumentCasts = new ObjectCast[parameters.Length];
        for (var i = 0; i < parameters.Length; ++i)
            argumentCasts[i] = new(parameters[i].ParameterType);
#if IS_NET_8_0_OR_GREATER
        if (member is ConstructorInfo constructor)
            constructorInvoker = ConstructorInvoker.Create(constructor);
        else
            methodInvoker = MethodInvoker.Create(member);
#else
        this.member = member;
#endif
    }

    readonly ObjectCast[] argumentCasts;
    readonly ObjectCast? instanceCast;
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

    object? CastInstance(object? instance) =>
        instanceCast is { } cast ? cast.Apply(instance ?? throw new NullReferenceException()) : null;

    internal object? Invoke(object? instance, object?[] arguments)
    {
        instance = CastInstance(instance);
        var castArguments = arguments;
        for (var i = 0; i < argumentCasts.Length; ++i)
        {
            var argument = arguments[i];
            var castArgument = argumentCasts[i].Apply(argument);
            if (!ReferenceEquals(castArgument, argument))
            {
                if (ReferenceEquals(castArguments, arguments))
                    castArguments = (object?[])arguments.Clone();
                castArguments[i] = castArgument;
            }
        }
#if IS_NET_8_0_OR_GREATER
        return constructorInvoker is { } constructor ? constructor.Invoke(castArguments) : methodInvoker!.Invoke(instance, castArguments);
#else
        return member is ConstructorInfo constructor ? constructor.Invoke(BindingFlags.DoNotWrapExceptions, null, castArguments, null) : member.Invoke(instance, BindingFlags.DoNotWrapExceptions, null, castArguments, null);
#endif
    }

    internal object? InvokeWithNoArgument(object? instance)
    {
#if IS_NET_8_0_OR_GREATER
        instance = CastInstance(instance);
        return constructorInvoker is { } constructor ? constructor.Invoke() : methodInvoker!.Invoke(instance);
#else
        return Invoke(instance, []);
#endif
    }

    internal object? InvokeWithOneArgument(object? instance, object? argument0)
    {
#if IS_NET_8_0_OR_GREATER
        instance = CastInstance(instance);
        argument0 = argumentCasts[0].Apply(argument0);
        return constructorInvoker is { } constructor ? constructor.Invoke(argument0) : methodInvoker!.Invoke(instance, argument0);
#else
        return Invoke(instance, [argument0]);
#endif
    }

    internal object? InvokeWithTwoArguments(object? instance, object? argument0, object? argument1)
    {
#if IS_NET_8_0_OR_GREATER
        instance = CastInstance(instance);
        argument0 = argumentCasts[0].Apply(argument0);
        argument1 = argumentCasts[1].Apply(argument1);
        return constructorInvoker is { } constructor ? constructor.Invoke(argument0, argument1) : methodInvoker!.Invoke(instance, argument0, argument1);
#else
        return Invoke(instance, [argument0, argument1]);
#endif
    }
}
