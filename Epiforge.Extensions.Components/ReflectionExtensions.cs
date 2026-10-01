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

    static bool IsNonPrintableAsciiCharacter(char c) =>
        c is < ' ' or > '~';

    static bool ShouldEscapeCharacter(char c) =>
        c switch
        {
            '\0' or '\a' or '\b' or '\f' or '\n' or '\r' or '\t' or '\v' or '\\' or '\'' or '\"' => true,
            _ => false
        };

    static T? GetDefaultValue<T>() =>
        default;

    [RequiresDynamicCode("Constructs a generic method for the type")]
    static MethodInfo GetDefaultValueByTypeValueFactory(Type type) =>
        typeof(ReflectionExtensions).GetMethod(nameof(GetDefaultValue), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(type);

    /// <summary>
    /// Returns the default value for the specified type as quickly as possible
    /// </summary>
    /// <param name="type">The type</param>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The code which needs dynamic code runs only where RuntimeFeature.IsDynamicCodeSupported is true, which the analyzer recognizes as a guard from .NET 9 on")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "The default of a value type is its instance with every field zeroed, which no constructor makes, so nothing a constructor needs has to survive trimming")]
    public static object? FastDefault(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsValueType)
            return null;
        if (type == typeof(void) || type.IsByRefLike || type.ContainsGenericParameters)
            throw new ArgumentException($"{type} has no default value which can be boxed", nameof(type));
        if (RuntimeFeature.IsDynamicCodeSupported)
            return getDefaultValueByType.GetOrAdd(type, GetDefaultValueByTypeValueFactory).FastInvoke(null);
        return Nullable.GetUnderlyingType(type) is null ? RuntimeHelpers.GetUninitializedObject(type) : null;
    }

    /// <summary>
    /// Returns the value for the specified property of the specified object as quickly as possible
    /// </summary>
    /// <param name="property">The property of which to get the value</param>
    /// <param name="instance">The object from which to get the value (if the property is static, this argument is ignored)</param>
    /// <param name="index">Optional index values for indexed properties</param>
    /// <remarks>An exception thrown by the property's get method is propagated as it was thrown rather than wrapped</remarks>
    public static object? FastGetValue(this PropertyInfo property, object? instance, params object?[] index)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.GetValue(instance, BindingFlags.DoNotWrapExceptions, null, index, null);
    }

    /// <summary>
    /// Invokes the constructor reflected by the instance that has the specified parameters, as quickly as possible
    /// </summary>
    /// <param name="constructor">The constructor to invoke</param>
    /// <param name="arguments">An argument list for the invoked constructor</param>
    /// <remarks>An exception thrown by the constructor is propagated as it was thrown rather than wrapped</remarks>
    public static object? FastInvoke(this ConstructorInfo constructor, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        return FastInvoker.Of(constructor).Invoke(null, arguments ?? []);
    }

    /// <summary>
    /// Invokes the method represented by the current instance, using the specified parameters, as quickly as possible
    /// </summary>
    /// <param name="method">The method to invoke</param>
    /// <param name="instance">The object on which to invoke the method (if the method is static, this argument is ignored)</param>
    /// <param name="arguments">An argument list for the invoked method</param>
    /// <remarks>An exception thrown by the method is propagated as it was thrown rather than wrapped</remarks>
    public static object? FastInvoke(this MethodInfo method, object? instance, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(method);
        return FastInvoker.Of(method).Invoke(instance, arguments ?? []);
    }

    /// <summary>
    /// Gets an invoker for the specified constructor, which resolves the invocation once instead of on every call
    /// </summary>
    /// <param name="constructor">The constructor</param>
    public static FastInvoker GetFastInvoker(this ConstructorInfo constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        return FastInvoker.Of(constructor);
    }

    /// <summary>
    /// Gets an invoker for the specified method, which resolves the invocation once instead of on every call
    /// </summary>
    /// <param name="method">The method</param>
    public static FastInvoker GetFastInvoker(this MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return FastInvoker.Of(method);
    }

    /// <summary>
    /// Sets the value for the specified property of the specified object as quickly as possible
    /// </summary>
    /// <param name="property">The property of which to set the value</param>
    /// <param name="instance">The object on which to set the value (if the property is static, this argument is ignored)</param>
    /// <param name="value">The value to set</param>
    /// <param name="index">Optional index values for indexed properties</param>
    /// <remarks>An exception thrown by the property's set method is propagated as it was thrown rather than wrapped</remarks>
    public static void FastSetValue(this PropertyInfo property, object? instance, object? value, params object?[] index)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.SetValue(instance, value, BindingFlags.DoNotWrapExceptions, null, index, null);
    }

    /// <summary>
    /// Searches for the events of the current <see cref="Type"/>, including interfaces and interface inheritance, using the specified binding constraints
    /// </summary>
    /// <param name="type">The <see cref="Type"/></param>
    /// <param name="bindingAttr">A bitwise combination of the enumeration values that specify how the search is conducted</param>
    [RequiresUnreferencedCode("Reflects over the interfaces the type implements and their members, which trimming can remove")]
    public static EventInfo[] GetImplementationEvents(this Type type, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsInterface)
        {
            var eventInfos = new List<EventInfo>();
            var considered = new HashSet<Type>();
            var queue = new Queue<Type>();
            considered.Add(type);
            queue.Enqueue(type);
            while (queue.Count > 0)
            {
                var subType = queue.Dequeue();
                foreach (var subInterface in subType.GetInterfaces())
                {
                    if (considered.Contains(subInterface))
                        continue;
                    considered.Add(subInterface);
                    queue.Enqueue(subInterface);
                }
                eventInfos.InsertRange(0, subType.GetEvents(bindingAttr).Where(x => !eventInfos.Contains(x)));
            }
            return [..eventInfos];
        }
        return type.GetEvents(bindingAttr);
    }

    /// <summary>
    /// Searches for the methods of the current <see cref="Type"/>, including interfaces and interface inheritance, using the specified binding constraints
    /// </summary>
    /// <param name="type">The <see cref="Type"/></param>
    /// <param name="bindingAttr">A bitwise combination of the enumeration values that specify how the search is conducted</param>
    [RequiresUnreferencedCode("Reflects over the interfaces the type implements and their members, which trimming can remove")]
    public static MethodInfo[] GetImplementationMethods(this Type type, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsInterface)
        {
            var methodInfos = new List<MethodInfo>();
            var considered = new HashSet<Type>();
            var queue = new Queue<Type>();
            considered.Add(type);
            queue.Enqueue(type);
            while (queue.Count > 0)
            {
                var subType = queue.Dequeue();
                foreach (var subInterface in subType.GetInterfaces())
                {
                    if (considered.Contains(subInterface))
                        continue;
                    considered.Add(subInterface);
                    queue.Enqueue(subInterface);
                }
                methodInfos.InsertRange(0, subType.GetMethods(bindingAttr).Where(x => !methodInfos.Contains(x)));
            }
            return [..methodInfos];
        }
        return type.GetMethods(bindingAttr);
    }

    /// <summary>
    /// Searches for the properties of the current <see cref="Type"/>, including interfaces and interface inheritance, using the specified binding constraints
    /// </summary>
    /// <param name="type">The <see cref="Type"/></param>
    /// <param name="bindingAttr">A bitwise combination of the enumeration values that specify how the search is conducted</param>
    [RequiresUnreferencedCode("Reflects over the interfaces the type implements and their members, which trimming can remove")]
    public static PropertyInfo[] GetImplementationProperties(this Type type, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsInterface)
        {
            var propertyInfos = new List<PropertyInfo>();
            var considered = new HashSet<Type>();
            var queue = new Queue<Type>();
            considered.Add(type);
            queue.Enqueue(type);
            while (queue.Count > 0)
            {
                var subType = queue.Dequeue();
                foreach (var subInterface in subType.GetInterfaces())
                {
                    if (considered.Contains(subInterface))
                        continue;
                    considered.Add(subInterface);
                    queue.Enqueue(subInterface);
                }
                propertyInfos.InsertRange(0, subType.GetProperties(bindingAttr).Where(x => !propertyInfos.Contains(x)));
            }
            return [..propertyInfos];
        }
        return type.GetProperties(bindingAttr);
    }

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
