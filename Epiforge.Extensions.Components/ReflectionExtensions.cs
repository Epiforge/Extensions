namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides extension methods for reflection types
/// </summary>
public static class ReflectionExtensions
{
    static readonly ConcurrentDictionary<Type, MethodInfo> getDefaultValueByType = new();

    static string EscapeCharacter(char c)
    {
        return c switch
        {
            '\0' => "\\0",
            '\a' => "\\a",
            '\b' => "\\b",
            '\f' => "\\f",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\v' => "\\v",
            '\\' => "\\\\",
            '\'' => "\\'",
            '\"' => "\\\"",
            var nonPrintable when IsNonPrintableAsciiCharacter(nonPrintable) => $"\\x{(int)nonPrintable:x2}",
            _ => throw new ArgumentException($"Invalid character: {c}")
        };
    }

    static string EscapeStringLiteral(string input)
    {
        var escapedString = new StringBuilder();
        for (int i = 0, ii = input.Length; i < ii; ++i)
        {
            var c = input[i];
            if (ShouldEscapeCharacter(c))
                escapedString.Append(EscapeCharacter(c));
            else
                escapedString.Append(c);
        }
        return escapedString.ToString();
    }

#if IS_NET_STANDARD_2_1_OR_GREATER
    static T? GetDefaultValue<T>() =>
        default;
#endif

    static MethodInfo GetDefaultValueByTypeValueFactory(Type type) =>
#if IS_NET_STANDARD_2_1_OR_GREATER
        typeof(ReflectionExtensions).GetMethod(nameof(GetDefaultValue), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(type);
#else
        typeof(ReflectionExtensions).GetMethod(nameof(GetDefaultValue), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(type);
#endif

#if !IS_NET_7_0_OR_GREATER
    delegate object InvokeConstructorDelegate(object?[] arguments);
    delegate object? InvokeMethodDelegate(object? instance, object?[] arguments);

    static readonly ConcurrentDictionary<ConstructorInfo, InvokeConstructorDelegate> invokeConstructorDelegateByConstructor = new();
    static readonly ConcurrentDictionary<MethodInfo, InvokeMethodDelegate> invokeMethodDelegateByMethod = new();

    static InvokeConstructorDelegate InvokeConstructorDelegateByConstructorValueFactory(ConstructorInfo constructor)
    {
        if (constructor.DeclaringType is not { } declaringType)
            throw new ArgumentException("Cannot handle constructors without declaring types");
        var dynamicMethod = new DynamicMethod($"CreateInstance_{declaringType.Name}", typeof(object), new[] { typeof(object[]) });
        var ilGenerator = dynamicMethod.GetILGenerator();
        var parameters = constructor.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldc_I4, i);
            ilGenerator.Emit(OpCodes.Ldelem_Ref);
            var parameterType = parameters[i].ParameterType;
            ilGenerator.Emit(parameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, parameterType);
        }
        ilGenerator.Emit(OpCodes.Newobj, constructor);
        if (declaringType.IsValueType)
            ilGenerator.Emit(OpCodes.Box, declaringType);
        ilGenerator.Emit(OpCodes.Ret);
        return (InvokeConstructorDelegate)dynamicMethod.CreateDelegate(typeof(InvokeConstructorDelegate));
    }

    static InvokeMethodDelegate InvokeMethodDelegateByMethodValueFactory(MethodInfo method)
    {
        if (method.DeclaringType is not { } declaringType)
            throw new ArgumentException("Cannot handle methods without declaring types");
        var dynamicMethod = new DynamicMethod($"Invoke_{method.Name}", typeof(object), new[] { typeof(object), typeof(object[]) });
        var ilGenerator = dynamicMethod.GetILGenerator();
        if (!method.IsStatic)
        {
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(declaringType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, declaringType);
        }
        var parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Ldc_I4, i);
            ilGenerator.Emit(OpCodes.Ldelem_Ref);
            var parameterType = parameters[i].ParameterType;
            ilGenerator.Emit(parameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, parameterType);
        }
        ilGenerator.Emit(method.IsStatic || declaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, method);
        var returnType = method.ReturnType;
        if (returnType == typeof(void))
            ilGenerator.Emit(OpCodes.Ldnull);
        else if (returnType.IsValueType)
            ilGenerator.Emit(OpCodes.Box, method.ReturnType);
        ilGenerator.Emit(OpCodes.Ret);
        return (InvokeMethodDelegate)dynamicMethod.CreateDelegate(typeof(InvokeMethodDelegate));
    }
#endif

    /// <summary>
    /// Returns the default value for the specified type as quickly as possible
    /// </summary>
    /// <param name="type">The type</param>
    public static object? FastDefault(this Type type)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type is null)
            throw new ArgumentNullException(nameof(type));
#endif
        return getDefaultValueByType.GetOrAdd(type, GetDefaultValueByTypeValueFactory).FastInvoke(null);
    }

    /// <summary>
    /// Returns the value for the specified property of the specified object as quickly as possible
    /// </summary>
    /// <param name="property">The property of which to get the value</param>
    /// <param name="instance">The object from which to get the value (if the property is static, this argument is ignored)</param>
    /// <param name="index">Optional index values for indexed properties</param>
    public static object? FastGetValue(this PropertyInfo property, object? instance, params object?[] index)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(property);
#else
        if (property is null)
            throw new ArgumentNullException(nameof(property));
#endif
#if IS_NET_7_0_OR_GREATER
        return property.GetValue(instance, index);
#else
        if (property.GetMethod is not { } getMethod)
            throw new ArgumentException("Cannot handle properties without getters");
        return FastInvoke(getMethod, instance, index);
#endif
    }

    /// <summary>
    /// Invokes the constructor reflected by the instance that has the specified parameters, as quickly as possible
    /// </summary>
    /// <param name="constructor">The constructor to invoke</param>
    /// <param name="arguments">An argument list for the invoked constructor</param>
    public static object? FastInvoke(this ConstructorInfo constructor, params object?[] arguments)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(constructor);
#else
        if (constructor is null)
            throw new ArgumentNullException(nameof(constructor));
#endif
#if IS_NET_7_0_OR_GREATER
        return constructor.Invoke(arguments);
#else
        return invokeConstructorDelegateByConstructor.GetOrAdd(constructor, InvokeConstructorDelegateByConstructorValueFactory)(arguments);
#endif
    }

    /// <summary>
    /// Invokes the method represented by the current instance, using the specified parameters, as quickly as possible
    /// </summary>
    /// <param name="method">The method to invoke</param>
    /// <param name="instance">The object on which to invoke the method (if the method is static, this argument is ignored)</param>
    /// <param name="arguments">An argument list for the invoked method</param>
    public static object? FastInvoke(this MethodInfo method, object? instance, params object?[] arguments)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(method);
#else
        if (method is null)
            throw new ArgumentNullException(nameof(method));
#endif
#if IS_NET_7_0_OR_GREATER
        return method.Invoke(instance, arguments);
#else
        return invokeMethodDelegateByMethod.GetOrAdd(method, InvokeMethodDelegateByMethodValueFactory)(instance, arguments);
#endif
    }

    /// <summary>
    /// Sets the value for the specified property of the specified object as quickly as possible
    /// </summary>
    /// <param name="property">The property of which to set the value</param>
    /// <param name="instance">The object on which to set the value (if the property is static, this argument is ignored)</param>
    /// <param name="value">The value to set</param>
    /// <param name="index">Optional index values for indexed properties</param>
    public static void FastSetValue(this PropertyInfo property, object? instance, object? value, params object?[] index)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(property);
#else
        if (property is null)
            throw new ArgumentNullException(nameof(property));
#endif
#if IS_NET_7_0_OR_GREATER
        property.SetValue(instance, value, index);
#else
        if (property.SetMethod is not { } setMethod)
            throw new ArgumentException("Cannot handle properties without setters");
        FastInvoke(setMethod, instance, index.Concat(new object?[] { value }).ToArray());
#endif
    }

#if !IS_NET_STANDARD_2_1_OR_GREATER
    /// <summary>
    /// This method is used to perform silly reflection tricks and should not be used by callers (use the default keyword in your own code instead)
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    [Obsolete("This method is used to perform silly reflection tricks and should not be used by callers (use the default keyword in your own code instead)", true)]
    public static T? GetDefaultValue<T>() =>
        default;
#endif

    static bool IsNonPrintableAsciiCharacter(char c) =>
        c is < ' ' or > '~';

    static bool ShouldEscapeCharacter(char c) =>
        c switch
        {
            '\0' or '\a' or '\b' or '\f' or '\n' or '\r' or '\t' or '\v' or '\\' or '\'' or '\"' => true,
            _ => false
        };

    /// <summary>
    /// Produces a string representation of the specified object that can be used as a literal in C# source code
    /// </summary>
    /// <param name="obj">The object</param>
    public static string ToObjectLiteral(this object? obj) =>
        obj switch
        {
            bool b => b ? "true" : "false",
            byte b => $"(byte)0x{b:X2}",
            sbyte sb => $"(sbyte)0x{sb:X2}",
            short s => $"(short){s}",
            ushort us => $"(ushort){us}",
            int i => i.ToString(),
            uint ui => $"{ui}U",
            long l => $"{l}L",
            ulong ul => $"{ul}UL",
            float f => $"{f}F",
            double d => $"{d}D",
            decimal d => $"{d}M",
            char shouldEscape when ShouldEscapeCharacter(shouldEscape) => $"'{EscapeCharacter(shouldEscape)}'",
            char nonPrintable when IsNonPrintableAsciiCharacter(nonPrintable) => $"'\\u{(int)nonPrintable:X4}'",
            Guid guid => $"new Guid(\"{guid}\")",
            DateTime dateTime => $"new DateTime({dateTime.Ticks}L, DateTimeKind.{dateTime.Kind})",
            TimeSpan timeSpan => $"new TimeSpan({timeSpan.Ticks}L)",
            DateTimeOffset dateTimeOffset => $"new DateTimeOffset({dateTimeOffset.Ticks}L, new TimeSpan({dateTimeOffset.Offset.Ticks}L))",
            Enum e => $"{e.GetType().Name}.{e}",
            char c => $"'{c}'",
            string s => $"\"{EscapeStringLiteral(s)}\"",
            null => "null",
            IDictionary dictionary when obj.GetType() is { } type && type.IsConstructedGenericType => $"new {type.GetGenericTypeDefinition().Name.Split('`')[0]}<{string.Join(", ", type.GenericTypeArguments.Select(gta => gta.Name))}> {{ {string.Join(", ", dictionary.Keys.Cast<object>().Select(key => $"{{ {ToObjectLiteral(key)}, {ToObjectLiteral(dictionary[key])} }}"))} }}",
            IEnumerable enumerable when obj.GetType() is { } type && type.IsConstructedGenericType => $"new {type.GetGenericTypeDefinition().Name.Split('`')[0]}<{type.GetGenericArguments()[0].Name}> {{ {string.Join(", ", enumerable.Cast<object>().Select(ToObjectLiteral))} }}",
            IEnumerable enumerable when obj.GetType() is { } type && type.IsArray => $"new {type.GetElementType()?.Name ?? "object"}[] {{ {string.Join(", ", enumerable.Cast<object>().Select(ToObjectLiteral))} }}",
            _ => obj.ToString() ?? "null"
        };
}
