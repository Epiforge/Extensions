namespace Epiforge.Extensions.Components;

/// <summary>
/// Invokes a constructor or a method as quickly as possible, resolving the invocation once so that repeated calls neither look it up nor build an argument array
/// </summary>
/// <remarks>
/// <see cref="ReflectionExtensions.FastInvoke(MethodInfo, object?, object?[])"/> looks its delegate up on every call, and that lookup costs more than half of what invoking through the delegate saves; a caller which invokes the same member repeatedly should resolve one of these once and keep it
/// </remarks>
public sealed class FastInvoker
{
    delegate object? InvokeWithArguments(object? instance, object?[] arguments);

    delegate object? InvokeWithNoArgument(object? instance);

    delegate object? InvokeWithOneArgument(object? instance, object? argument0);

    delegate object? InvokeWithTwoArguments(object? instance, object? argument0, object? argument1);

    static readonly ConcurrentDictionary<ConstructorInfo, FastInvoker> invokersByConstructor = new();
    static readonly ConcurrentDictionary<MethodInfo, FastInvoker> invokersByMethod = new();

    static FastInvoker InvokersByConstructorValueFactory(ConstructorInfo constructor) =>
        new(constructor, constructor.DeclaringType ?? throw new ArgumentException("Cannot handle constructors without declaring types", nameof(constructor)));

    static FastInvoker InvokersByMethodValueFactory(MethodInfo method) =>
        new(method, method.DeclaringType ?? throw new ArgumentException("Cannot handle methods without declaring types", nameof(method)));

    internal static FastInvoker Of(ConstructorInfo constructor) =>
        invokersByConstructor.GetOrAdd(constructor, InvokersByConstructorValueFactory);

    internal static FastInvoker Of(MethodInfo method) =>
        invokersByMethod.GetOrAdd(method, InvokersByMethodValueFactory);

    FastInvoker(MethodBase member, Type declaringType)
    {
        var parameters = member.GetParameters();
        ParameterCount = parameters.Length;
        var packed = ParameterCount > 2;
        Type[] signature;
        if (packed)
            signature = [typeof(object), typeof(object[])];
        else
        {
            signature = new Type[ParameterCount + 1];
            for (var i = 0; i < signature.Length; ++i)
                signature[i] = typeof(object);
        }
        var dynamicMethod = new DynamicMethod($"FastInvoke_{member.Name}", typeof(object), signature);
        var ilGenerator = dynamicMethod.GetILGenerator();
        if (member is MethodInfo { IsStatic: false })
        {
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(declaringType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, declaringType);
        }
        for (var i = 0; i < parameters.Length; ++i)
        {
            if (packed)
            {
                ilGenerator.Emit(OpCodes.Ldarg_1);
                ilGenerator.Emit(OpCodes.Ldc_I4, i);
                ilGenerator.Emit(OpCodes.Ldelem_Ref);
            }
            else
                ilGenerator.Emit(OpCodes.Ldarg_S, (byte)(i + 1));
            var parameterType = parameters[i].ParameterType;
            ilGenerator.Emit(parameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, parameterType);
        }
        if (member is ConstructorInfo constructorInfo)
        {
            ilGenerator.Emit(OpCodes.Newobj, constructorInfo);
            if (declaringType.IsValueType)
                ilGenerator.Emit(OpCodes.Box, declaringType);
        }
        else
        {
            var methodInfo = (MethodInfo)member;
            ilGenerator.Emit(methodInfo.IsStatic || declaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, methodInfo);
            if (methodInfo.ReturnType == typeof(void))
                ilGenerator.Emit(OpCodes.Ldnull);
            else if (methodInfo.ReturnType.IsValueType)
                ilGenerator.Emit(OpCodes.Box, methodInfo.ReturnType);
        }
        ilGenerator.Emit(OpCodes.Ret);
        if (packed)
            withArguments = (InvokeWithArguments)dynamicMethod.CreateDelegate(typeof(InvokeWithArguments));
        else if (ParameterCount == 0)
            withNoArgument = (InvokeWithNoArgument)dynamicMethod.CreateDelegate(typeof(InvokeWithNoArgument));
        else if (ParameterCount == 1)
            withOneArgument = (InvokeWithOneArgument)dynamicMethod.CreateDelegate(typeof(InvokeWithOneArgument));
        else
            withTwoArguments = (InvokeWithTwoArguments)dynamicMethod.CreateDelegate(typeof(InvokeWithTwoArguments));
    }

    readonly InvokeWithArguments? withArguments;
    readonly InvokeWithNoArgument? withNoArgument;
    readonly InvokeWithOneArgument? withOneArgument;
    readonly InvokeWithTwoArguments? withTwoArguments;

    /// <summary>
    /// Gets the number of parameters the constructor or method takes
    /// </summary>
    public int ParameterCount { get; }

    ArgumentException Mismatch(int argumentCount) =>
        new($"the member takes {ParameterCount} arguments and {argumentCount} were supplied");

    /// <summary>
    /// Invokes the constructor or method, which must take no parameters
    /// </summary>
    /// <param name="instance">The object on which to invoke the method, ignored for a constructor or a static method</param>
    public object? Invoke(object? instance) =>
        withNoArgument is { } invoke ? invoke(instance) : throw Mismatch(0);

    /// <summary>
    /// Invokes the constructor or method, which must take one parameter
    /// </summary>
    /// <param name="instance">The object on which to invoke the method, ignored for a constructor or a static method</param>
    /// <param name="argument0">The first argument</param>
    public object? Invoke(object? instance, object? argument0) =>
        withOneArgument is { } invoke ? invoke(instance, argument0) : throw Mismatch(1);

    /// <summary>
    /// Invokes the constructor or method, which must take two parameters
    /// </summary>
    /// <param name="instance">The object on which to invoke the method, ignored for a constructor or a static method</param>
    /// <param name="argument0">The first argument</param>
    /// <param name="argument1">The second argument</param>
    public object? Invoke(object? instance, object? argument0, object? argument1) =>
        withTwoArguments is { } invoke ? invoke(instance, argument0, argument1) : throw Mismatch(2);

    /// <summary>
    /// Invokes the constructor or method with an argument list of any length
    /// </summary>
    /// <param name="instance">The object on which to invoke the method, ignored for a constructor or a static method</param>
    /// <param name="arguments">An argument list, which must have as many entries as the member has parameters</param>
    public object? Invoke(object? instance, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length != ParameterCount)
            throw Mismatch(arguments.Length);
        if (withArguments is { } invoke)
            return invoke(instance, arguments);
        return ParameterCount switch
        {
            0 => withNoArgument!(instance),
            1 => withOneArgument!(instance, arguments[0]),
            _ => withTwoArguments!(instance, arguments[0], arguments[1])
        };
    }
}
