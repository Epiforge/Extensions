using Microsoft.Extensions.Logging;

namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Defines the options available when instantiating a new <see cref="IExpressionObserver"/>
/// </summary>
public class ExpressionObserverOptions
{
    internal static readonly ConcurrentDictionary<MethodInfo, MethodInfo> GenericMethodToGenericMethodDefinition = new();
    internal static readonly ConcurrentDictionary<MethodInfo, PropertyInfo?> PropertyGetMethodToProperty = new();

    internal static MethodInfo GetGenericMethodDefinitionFromGenericMethod(MethodInfo methodInfo) =>
        methodInfo.GetGenericMethodDefinition();

    internal static PropertyInfo? GetPropertyFromGetMethod(MethodInfo getMethod) =>
        getMethod.DeclaringType?.GetProperties().FirstOrDefault(property => property.GetMethod == getMethod);

    internal readonly ConcurrentDictionary<(Type type, EquatableList<Type> constuctorParameterTypes), byte> DisposeConstructedTypes = new();
    internal readonly ConcurrentDictionary<MethodInfo, byte> DisposeMethodReturnValues = new();
    internal readonly ConcurrentDictionary<PropertyInfo, byte> IgnoredPropertyChangeNotifications = new();

    /// <summary>
    /// Gets/sets whether the expression observer will block execution of the thread when it must asynchronously dispose of a previous value; the default is <c>false</c>
    /// </summary>
    public bool BlockOnAsyncDisposal { get; set; } = false;

    /// <summary>
    /// Gets/sets whether the expression observer will subscribe to <see cref="INotifyCollectionChanged.CollectionChanged" /> events of constant expression values when present and cause re-evaluations when they occur; the default is <c>true</c>
    /// </summary>
    public bool ConstantExpressionsListenForCollectionChanged { get; set; } = true;

    /// <summary>
    /// Gets/sets whether the expression observer will subscribe to <see cref="INotifyDictionaryChanged.DictionaryChanged" /> events of constant expression values when present and cause re-evaluations when they occur; the default is <c>true</c>
    /// </summary>
    public bool ConstantExpressionsListenForDictionaryChanged { get; set; } = true;

    /// <summary>
    /// Gets/sets whether the expression observer will dispose of objects it has constructed when the objects are replaced or otherwise discarded; the default is <c>true</c>
    /// </summary>
    public bool DisposeConstructedObjects { get; set; } = true;

    /// <summary>
    /// Gets/sets whether the expression observer will dispose of objects it has received as a result of invoking static (Shared in Visual Basic) methods when the objects are replaced or otherwise discarded; the default is <c>true</c>
    /// </summary>
    public bool DisposeStaticMethodReturnValues { get; set; } = true;

    /// <summary>
    /// Gets/sets the <see cref="ILogger"/> instance to which trace information will be written; the default is <c>null</c>
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Gets/sets whether the expression observer will subscribe to <see cref="INotifyCollectionChanged.CollectionChanged" /> events of constant expression values when present and retrieved from a field of a compiler-generated type and cause re-evaluations when they occur; the default is <c>true</c>
    /// </summary>
    public bool MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged { get; set; } = true;

    /// <summary>
    /// Gets/sets whether the expression observer will subscribe to <see cref="INotifyDictionaryChanged.DictionaryChanged" /> events of constant expression values when present and retrieved from a field of a compiler-generated type and cause re-evaluations when they occur; the default is <c>true</c>
    /// </summary>
    public bool MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged { get; set; } = true;

    /// <summary>
    /// Gets/sets the method that will be invoked during the observable expression creation process to optimize expressions (default is <c>null</c>)
    /// </summary>
    public Func<Expression, Expression>? Optimizer { get; set; }

    /// <summary>
    /// Gets/sets whether the expression observer will prefer asynchronous disposal over synchronous disposal when both interfaces are implemented; the default is <c>true</c>
    /// </summary>
    public bool PreferAsyncDisposal { get; set; } = true;

    /// <summary>
    /// Specifies that the expression observer will dispose of objects it has created of the specified type and using constructor arguments of the specified types when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="type">The type of object created</param>
    /// <param name="constuctorParameterTypes">The types of the arguments passed to the constructor, in order</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool AddConstructedTypeDisposal(Type type, params Type[] constuctorParameterTypes)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(constuctorParameterTypes);
        if (constuctorParameterTypes.Any(constructorParameterType => constructorParameterType is null))
            throw new ArgumentException("One or more constructor parameter types are null", nameof(constuctorParameterTypes));
        return DisposeConstructedTypes.TryAdd((type, new EquatableList<Type>(constuctorParameterTypes)), 1);
    }

    /// <summary>
    /// Specifies that the expression observer will dispose of objects they have created using the specified constructor when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="constructor">The constructor</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool AddConstructedTypeDisposal(ConstructorInfo constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        if (constructor.DeclaringType is not { } declaringType)
            throw new ArgumentException("The constructor does not have a declaring type", nameof(constructor));
        return DisposeConstructedTypes.TryAdd((declaringType, new EquatableList<Type>([..constructor.GetParameters().Select(parameterInfo => parameterInfo.ParameterType)])), 1);
    }

    /// <summary>
    /// Specifies that the expression observer will dispose of objects it has received as a result of invoking a constructor, operator, or method, or getting the value of a property or indexer when the objects are replaced or otherwise discarded
    /// </summary>
    /// <typeparam name="T">The type of the objects</typeparam>
    /// <param name="lambda">An expression indicating the kind of behavior that is yielding the objects</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool AddExpressionValueDisposal<T>(Expression<Func<T>> lambda) =>
        AddExpressionValueDisposal(lambda, false);

    /// <summary>
    /// Specifies that the expression observer will dispose of objects it has received as a result of invoking a constructor, operator, or method, or getting the value of a property or indexer when the objects are replaced or otherwise discarded
    /// </summary>
    /// <typeparam name="T">The type of the objects</typeparam>
    /// <param name="lambda">An expression indicating the kind of behavior that is yielding the objects</param>
    /// <param name="useGenericDefinition">Whether or not objects created by operators, methods, or property or indexer getters will be disposed regardless of generic type arguments used by the source</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool AddExpressionValueDisposal<T>(Expression<Func<T>> lambda, bool useGenericDefinition)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        return lambda.Body switch
        {
            BinaryExpression binary when binary.Method is { } method => AddMethodReturnValueDisposal(method, useGenericDefinition),
            IndexExpression index when index.Indexer is { } indexer => AddPropertyValueDisposal(indexer, useGenericDefinition),
            NewExpression @new when !useGenericDefinition && @new.Constructor is { } constructor => AddConstructedTypeDisposal(constructor),
            MemberExpression member when member.Member is PropertyInfo property => AddPropertyValueDisposal(property, useGenericDefinition),
            MethodCallExpression methodCallExpressionForPropertyGet when methodCallExpressionForPropertyGet.Method is { } method && PropertyGetMethodToProperty.GetOrAdd(method, GetPropertyFromGetMethod) is { } property => AddPropertyValueDisposal(property, useGenericDefinition),
            MethodCallExpression methodCall when methodCall.Method is { } method => AddMethodReturnValueDisposal(method, useGenericDefinition),
            UnaryExpression unary when unary.Method is { } method => AddMethodReturnValueDisposal(method, useGenericDefinition),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Specifies that property change notifications for the specified property will be ignored
    /// </summary>
    /// <param name="property">The property for which property change notifications will be ignored</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool AddIgnoredPropertyChangeNotification(PropertyInfo property) =>
        IgnoredPropertyChangeNotifications.TryAdd(property, 1);

    /// <summary>
    /// Specifies that the expression observer will dispose of objects they have received as a result of invoking a specified method when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="method">The method yielding the objects</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool AddMethodReturnValueDisposal(MethodInfo method) =>
        AddMethodReturnValueDisposal(method, false);

    /// <summary>
    /// Specifies that the expression observer will dispose of objects they have received as a result of invoking a specified method when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="method">The method yielding the objects</param>
    /// <param name="useGenericDefinition">Whether or not objects created by the method will be disposed regardless of generic type arguments used to make the method</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool AddMethodReturnValueDisposal(MethodInfo method, bool useGenericDefinition)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (useGenericDefinition)
        {
            if (!method.IsGenericMethod)
                throw new ArgumentException("the method specified is not generic", nameof(useGenericDefinition));
            method = GenericMethodToGenericMethodDefinition.GetOrAdd(method, GetGenericMethodDefinitionFromGenericMethod);
        }
        return DisposeMethodReturnValues.TryAdd(method, 1);
    }

    /// <summary>
    /// Specifies that the expression observer will dispose of objects they have received as a result of getting the value of a specified property when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="property">The property yielding the objects</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool AddPropertyValueDisposal(PropertyInfo property) =>
        AddPropertyValueDisposal(property, false);

    /// <summary>
    /// Specifies that the expression observer will dispose of objects they have received as a result of getting the value of a specified property when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="property">The property yielding the objects</param>
    /// <param name="useGenericDefinition">Whether or not objects created by the property getter will be disposed regardless of generic type arguments used to make the property getter</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool AddPropertyValueDisposal(PropertyInfo property, bool useGenericDefinition)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (property.GetMethod is not { } getMethod)
            throw new ArgumentException("the property specified does not have a getter", nameof(property));
        return AddMethodReturnValueDisposal(getMethod, useGenericDefinition);
    }

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have created of the specified type and using constructor arguments of the specified types when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="type">The type of object created</param>
    /// <param name="constructorParameterTypes">The types of the arguments passed to the constructor, in order</param>
    /// <returns><c>true</c> if objects from this source will be disposed; otherwise, <c>false</c></returns>
    public bool IsConstructedTypeDisposed(Type type, params Type[] constructorParameterTypes) =>
        DisposeConstructedObjects || DisposeConstructedTypes.ContainsKey((type, new EquatableList<Type>(constructorParameterTypes)));

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have created using the specified constructor when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="constructor">The constructor</param>
    /// <returns><c>true</c> if objects from this source will be disposed; otherwise, <c>false</c></returns>
    public bool IsConstructedTypeDisposed(ConstructorInfo constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        if (constructor.DeclaringType is not { } declaringType)
            throw new ArgumentException("the constructor specified does not have a declaring type", nameof(constructor));
        return DisposeConstructedTypes.ContainsKey((declaringType, new EquatableList<Type>([..constructor.GetParameters().Select(parameterInfo => parameterInfo.ParameterType)])));
    }

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have received as a result of invoking a constructor, operator, or method, or getting the value of a property or indexer when the objects are replaced or otherwise discarded
    /// </summary>
    /// <typeparam name="T">The type of the objects</typeparam>
    /// <param name="lambda">An expression indicating the kind of behavior that is yielding the objects</param>
    /// <returns><c>true</c> if objects from this source will be disposed; otherwise, <c>false</c></returns>
    public bool IsExpressionValueDisposed<T>(Expression<Func<T>> lambda)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        return lambda.Body switch
        {
            BinaryExpression binary when binary.Method is { } method => IsMethodReturnValueDisposed(method),
            IndexExpression index when index.Indexer is { } indexer => IsPropertyValueDisposed(indexer),
            NewExpression @new when @new.Constructor is { } constructor => IsConstructedTypeDisposed(constructor),
            MemberExpression member when member.Member is PropertyInfo property => IsPropertyValueDisposed(property),
            MethodCallExpression methodCallExpressionForPropertyGet when methodCallExpressionForPropertyGet.Method is { } method && PropertyGetMethodToProperty.GetOrAdd(method, GetPropertyFromGetMethod) is { } property => IsPropertyValueDisposed(property),
            MethodCallExpression methodCall when methodCall.Method is { } method => IsMethodReturnValueDisposed(method),
            UnaryExpression unary when unary.Method is { } method => IsMethodReturnValueDisposed(method),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Gets whether the expression observer will ignore property change notifications for the specified property
    /// </summary>
    /// <param name="property">The property</param>
    /// <returns><c>true</c> if property change notifications for this property will be ignored; otherwise, <c>false</c></returns>
    public bool IsIgnoredPropertyChangeNotification(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return IgnoredPropertyChangeNotifications.ContainsKey(property);
    }

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have received as a result of invoking a specified method when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="method">The method yielding the objects</param>
    /// <returns><c>true</c> if objects from this source will be disposed; otherwise, <c>false</c></returns>
    public bool IsMethodReturnValueDisposed(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return method.IsStatic && DisposeStaticMethodReturnValues || DisposeMethodReturnValues.ContainsKey(method) || method.IsGenericMethod && DisposeMethodReturnValues.ContainsKey(GenericMethodToGenericMethodDefinition.GetOrAdd(method, GetGenericMethodDefinitionFromGenericMethod));
    }

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have received as a result of getting the value of a specified property when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="property">The property yielding the objects</param>
    /// <returns><c>true</c> if objects from this source will be disposed; otherwise, <c>false</c></returns>
    public bool IsPropertyValueDisposed(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (property.GetMethod is not { } getMethod)
            throw new ArgumentException("the property specified does not have a getter", nameof(property));
        return IsMethodReturnValueDisposed(getMethod);
    }

    /// <summary>
    /// Specifies that the expression observer will not dispose of objects they have created of the specified type and using constructor arguments of the specified types when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="type">The type of object created</param>
    /// <param name="constuctorParameterTypes">The types of the arguments passed to the constructor, in order</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool RemoveConstructedTypeDisposal(Type type, params Type[] constuctorParameterTypes) =>
        DisposeConstructedTypes.TryRemove((type, new EquatableList<Type>(constuctorParameterTypes)), out _);

    /// <summary>
    /// Specifies that the expression observer will not dispose of objects they have created using the specified constructor when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="constructor">The constructor</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool RemoveConstructedTypeDisposal(ConstructorInfo constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        if (constructor.DeclaringType is not { } declaringType)
            throw new ArgumentException("the constructor specified does not have a declaring type", nameof(constructor));
        return DisposeConstructedTypes.TryRemove((declaringType, new EquatableList<Type>([..constructor.GetParameters().Select(parameterInfo => parameterInfo.ParameterType)])), out _);
    }

    /// <summary>
    /// Specifies that the expression observer will not dispose of objects they have received as a result of invoking a constructor, operator, or method, or getting the value of a property or indexer when the objects are replaced or otherwise discarded
    /// </summary>
    /// <typeparam name="T">The type of the objects</typeparam>
    /// <param name="lambda">An expression indicating the kind of behavior that is yielding the objects</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool RemoveExpressionValueDisposal<T>(Expression<Func<T>> lambda)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        return lambda.Body switch
        {
            BinaryExpression binary when binary.Method is { } method => RemoveMethodReturnValueDisposal(method),
            IndexExpression index when index.Indexer is { } indexer => RemovePropertyValueDisposal(indexer),
            NewExpression @new when @new.Constructor is { } constructor => RemoveConstructedTypeDisposal(constructor),
            MemberExpression member when member.Member is PropertyInfo property => RemovePropertyValueDisposal(property),
            MethodCallExpression methodCallExpressionForPropertyGet when methodCallExpressionForPropertyGet.Method is { } method && PropertyGetMethodToProperty.GetOrAdd(method, GetPropertyFromGetMethod) is { } property => RemovePropertyValueDisposal(property),
            MethodCallExpression methodCall when methodCall.Method is { } method => RemoveMethodReturnValueDisposal(method),
            UnaryExpression unary when unary.Method is { } method => RemoveMethodReturnValueDisposal(method),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Specifies that the expression observer will not ignore property change notifications for the specified property
    /// </summary>
    /// <param name="property">The property</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool RemoveIgnoredPropertyChangeNotification(PropertyInfo property) =>
        IgnoredPropertyChangeNotifications.TryRemove(property, out _);

    /// <summary>
    /// Specifies that the expression observer will not dispose of objects they have received as a result of invoking a specified method when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="method">The method yielding the objects</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool RemoveMethodReturnValueDisposal(MethodInfo method) =>
        DisposeMethodReturnValues.TryRemove(method, out _);

    /// <summary>
    /// Specifies that active expressions using these options will not dispose of objects they have received as a result of getting the value of a specified property when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="property">The property yielding the objects</param>
    /// <returns><c>true</c> if this has resulted in a change in the options; otherwise, <c>false</c></returns>
    public bool RemovePropertyValueDisposal(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (property.GetMethod is not { } getMethod)
            throw new ArgumentException("the property specified does not have a getter", nameof(property));
        return IsMethodReturnValueDisposed(getMethod);
    }
}
