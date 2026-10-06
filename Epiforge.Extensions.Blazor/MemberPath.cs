namespace Epiforge.Extensions.Blazor;

/// <summary>
/// A member read at the end of a chain of members, shared by every expression reading that chain so that what is resolved for a member is resolved once per process rather than once per expression or per component
/// </summary>
sealed class MemberPath
{
    sealed class ContainerVerdict(Type containerType, bool isCompilerGenerated)
    {
        public readonly Type ContainerType = containerType;
        public readonly bool IsCompilerGenerated = isCompilerGenerated;
    }

    const int childIndexThreshold = 8;

    static readonly object readsOnlyBoxed = new();

    public static readonly MemberPath Closures = new(null, null);
    public static readonly MemberPath Observations = new(null, null);

    MemberPath(MemberPath? parent, MemberInfo? member)
    {
        this.parent = parent;
        this.member = member;
        Name = member?.Name ?? string.Empty;
    }

    Dictionary<MemberInfo, MemberPath>? childIndex;
    MemberPath[] children = [];
    ContainerVerdict? containerVerdict;
    readonly MemberInfo? member;
    readonly MemberPath? parent;
    Func<object, object?>? reader;
    MemberPath[]? steps;
    object? typedReader;
    Action<object, object?>? writer;

    public bool IsWritable =>
        member switch
        {
            FieldInfo fieldInfo => !fieldInfo.IsInitOnly && !fieldInfo.IsLiteral,
            PropertyInfo property => property.CanWrite,
            _ => false
        };

    public string Name { get; }

    public MemberPath[] Steps =>
        steps ??= CreateSteps();

    MemberPath AddChild(MemberInfo childMember)
    {
        lock (this)
        {
            var current = children;
            for (int i = 0, ii = current.Length; i < ii; ++i)
            {
                var child = current[i];
                if (child.member!.Equals(childMember))
                    return child;
            }
            var added = new MemberPath(this, childMember);
            var next = new MemberPath[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = added;
            if (next.Length > childIndexThreshold)
            {
                var index = new Dictionary<MemberInfo, MemberPath>(next.Length);
                foreach (var child in next)
                    index[child.member!] = child;
                Volatile.Write(ref childIndex, index);
            }
            Volatile.Write(ref children, next);
            return added;
        }
    }

    public MemberPath Child(MemberInfo childMember)
    {
        if (Volatile.Read(ref childIndex) is { } index)
            return index.TryGetValue(childMember, out var indexed) ? indexed : AddChild(childMember);
        var current = Volatile.Read(ref children);
        for (int i = 0, ii = current.Length; i < ii; ++i)
        {
            var child = current[i];
            if (ReferenceEquals(child.member, childMember))
                return child;
        }
        return AddChild(childMember);
    }

    Func<object, object?> CreateReader()
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            return EmitReader<object?>();
        if (member is FieldInfo fieldInfo)
            return fieldInfo.GetValue;
        var property = (PropertyInfo)member!;
        return target => property.GetValue(target, BindingFlags.DoNotWrapExceptions, null, null, null);
    }

    MemberPath[] CreateSteps()
    {
        var depth = 0;
        for (var path = this; path.member is not null; path = path.parent!)
            ++depth;
        var created = new MemberPath[depth];
        for (var path = this; path.member is not null; path = path.parent!)
            created[--depth] = path;
        return created;
    }

    Action<object, object?> CreateWriter()
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            return EmitWriter();
        if (member is FieldInfo fieldInfo)
            return fieldInfo.SetValue;
        var property = (PropertyInfo)member!;
        return (target, value) => property.SetValue(target, value, BindingFlags.DoNotWrapExceptions, null, null, null);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The code which needs dynamic code runs only where RuntimeFeature.IsDynamicCodeSupported is true, which the analyzer recognizes as a guard from .NET 9 on")]
    Func<object, TResult> EmitReader<TResult>()
    {
        var declaringType = member!.DeclaringType!;
        var method = new DynamicMethod($"Read_{member.Name}", typeof(TResult), [typeof(object)], typeof(MemberPath).Module, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(declaringType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, declaringType);
        Type memberType;
        if (member is FieldInfo fieldInfo)
        {
            il.Emit(OpCodes.Ldfld, fieldInfo);
            memberType = fieldInfo.FieldType;
        }
        else
        {
            var getter = ((PropertyInfo)member).GetGetMethod(true)!;
            il.Emit(declaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, getter);
            memberType = getter.ReturnType;
        }
        if (memberType.IsValueType && !typeof(TResult).IsValueType)
            il.Emit(OpCodes.Box, memberType);
        il.Emit(OpCodes.Ret);
        return (Func<object, TResult>)method.CreateDelegate(typeof(Func<object, TResult>));
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The code which needs dynamic code runs only where RuntimeFeature.IsDynamicCodeSupported is true, which the analyzer recognizes as a guard from .NET 9 on")]
    Action<object, object?> EmitWriter()
    {
        var declaringType = member!.DeclaringType!;
        var method = new DynamicMethod($"Write_{member.Name}", typeof(void), [typeof(object), typeof(object)], typeof(MemberPath).Module, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(declaringType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, declaringType);
        il.Emit(OpCodes.Ldarg_1);
        if (member is FieldInfo fieldInfo)
        {
            il.Emit(fieldInfo.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, fieldInfo.FieldType);
            il.Emit(OpCodes.Stfld, fieldInfo);
        }
        else
        {
            var property = (PropertyInfo)member;
            il.Emit(property.PropertyType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, property.PropertyType);
            il.Emit(declaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, property.GetSetMethod(true)!);
        }
        il.Emit(OpCodes.Ret);
        return (Action<object, object?>)method.CreateDelegate(typeof(Action<object, object?>));
    }

    /// <summary>
    /// Gets whether this member, read from an object of the specified type, is reading a compiler-generated closure, which is resolved rather than observed
    /// </summary>
    public bool IsReadFromCompilerGenerated(Type containerType)
    {
        var verdict = containerVerdict;
        if (verdict is null || verdict.ContainerType != containerType)
            containerVerdict = verdict = new(containerType, containerType.IsDefined(typeof(CompilerGeneratedAttribute), true));
        return verdict.IsCompilerGenerated;
    }

    public object? Read(object target) =>
        (reader ??= CreateReader())(target);

    public T Read<T>(object target)
    {
        var typed = typedReader;
        if (typed is Func<object, T> typedRead)
            return typedRead(target);
        if (typed is null)
        {
            var memberType = member is FieldInfo fieldInfo ? fieldInfo.FieldType : ((PropertyInfo)member!).PropertyType;
            if (RuntimeFeature.IsDynamicCodeSupported && (memberType == typeof(T) || (!memberType.IsValueType && !typeof(T).IsValueType)))
            {
                var emitted = EmitReader<T>();
                typedReader = emitted;
                return emitted(target);
            }
            typedReader = readsOnlyBoxed;
        }
        return (T)Read(target)!;
    }

    public void Write(object target, object? value) =>
        (writer ??= CreateWriter())(target, value);
}
