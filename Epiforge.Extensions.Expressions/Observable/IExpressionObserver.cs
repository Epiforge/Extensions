namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Represents an observer of expressions
/// </summary>
public interface IExpressionObserver
{
    /// <summary>
    /// Gets whether the expression observer will block execution of the thread when it must asynchronously dispose of a previous value; the default is <c>false</c>
    /// </summary>
    bool BlockOnAsyncDisposal { get; }

    /// <summary>
    /// Gets the number of cached observable expressions
    /// </summary>
    int CachedObservableExpressions { get; }

    /// <summary>
    /// Gets whether the expression observer will subscribe to <see cref="INotifyCollectionChanged.CollectionChanged" /> events of constant expression values when present and cause re-evaluations when they occur; the default is <c>true</c>
    /// </summary>
    bool ConstantExpressionsListenForCollectionChanged { get; }

    /// <summary>
    /// Gets whether the expression observer will subscribe to <see cref="INotifyDictionaryChanged.DictionaryChanged" /> events of constant expression values when present and cause re-evaluations when they occur; the default is <c>true</c>
    /// </summary>
    bool ConstantExpressionsListenForDictionaryChanged { get; }

    /// <summary>
    /// Gets whether the expression observer will dispose of objects it has constructed when the objects are replaced or otherwise discarded; the default is <c>true</c>
    /// </summary>
    bool DisposeConstructedObjects { get; }

    /// <summary>
    /// Gets whether the expression observer will dispose of objects it has received as a result of invoking static (Shared in Visual Basic) methods when the objects are replaced or otherwise discarded; the default is <c>true</c>
    /// </summary>
    bool DisposeStaticMethodReturnValues { get; }

    /// <summary>
    /// Gets the <see cref="ILogger"/> instance to which trace information will be written
    /// </summary>
    ILogger? Logger { get; }

    /// <summary>
    /// Gets whether the expression observer will subscribe to <see cref="INotifyCollectionChanged.CollectionChanged" /> events of constant expression values when present and retrieved from a field of a compiler-generated type and cause re-evaluations when they occur; the default is <c>true</c>
    /// </summary>
    bool MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged { get; }

    /// <summary>
    /// Gets whether the expression observer will subscribe to <see cref="INotifyDictionaryChanged.DictionaryChanged" /> events of constant expression values when present and retrieved from a field of a compiler-generated type and cause re-evaluations when they occur; the default is <c>true</c>
    /// </summary>
    bool MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged { get; }

    /// <summary>
    /// Gets the method that will be invoked during the observable expression creation process to optimize expressions (default is <c>null</c>)
    /// </summary>
    public Func<Expression, Expression>? Optimizer { get; }

    /// <summary>
    /// Gets whether the expression observer will prefer asynchronous disposal over synchronous disposal when both interfaces are implemented; the default is <c>true</c>
    /// </summary>
    bool PreferAsyncDisposal { get; }

    /// <summary>
    /// Returns a task which is only completed when the specified condition evaluates to <c>true</c>
    /// </summary>
    /// <param name="condition">The condition</param>
    Task ConditionAsync(Expression<Func<bool>> condition);

    /// <summary>
    /// Returns a task which is only completed when the specified condition evaluates to <c>true</c>
    /// </summary>
    /// <param name="condition">The condition</param>
    /// <param name="cancellationToken">A token which may cancel awaiting the condition</param>
    Task ConditionAsync(Expression<Func<bool>> condition, CancellationToken cancellationToken);

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have created of the specified type and using constructor arguments of the specified types when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="type">The type of object created</param>
    /// <param name="constructorParameterTypes">The types of the arguments passed to the constructor, in order</param>
    /// <returns><c>true</c> if objects from this source should be disposed; otherwise, <c>false</c></returns>
    bool IsConstructedTypeDisposed(Type type, params Type[] constructorParameterTypes);

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have created using the specified constructor when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="constructor">The constructor</param>
    /// <returns><c>true</c> if objects from this source should be disposed; otherwise, <c>false</c></returns>
    bool IsConstructedTypeDisposed(ConstructorInfo constructor);

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have received as a result of invoking a constructor, operator, or method, or getting the value of a property or indexer when the objects are replaced or otherwise discarded
    /// </summary>
    /// <typeparam name="T">The type of the objects</typeparam>
    /// <param name="lambda">An expression indicating the kind of behavior that is yielding the objects</param>
    /// <returns><c>true</c> if objects from this source should be disposed; otherwise, <c>false</c></returns>
    bool IsExpressionValueDisposed<T>(Expression<Func<T>> lambda);

    /// <summary>
    /// Gets whether the expression observer will ignore property change notifications for the specified property
    /// </summary>
    /// <param name="property">The property</param>
    /// <returns><c>true</c> if property change notifications for this property will be ignored; otherwise, <c>false</c></returns>
    bool IsIgnoredPropertyChangeNotification(PropertyInfo property);

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have received as a result of invoking a specified method when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="method">The method yielding the objects</param>
    /// <returns><c>true</c> if objects from this source should be disposed; otherwise, <c>false</c></returns>
    bool IsMethodReturnValueDisposed(MethodInfo method);

    /// <summary>
    /// Gets whether the expression observer will dispose of objects they have received as a result of getting the value of a specified property when the objects are replaced or otherwise discarded
    /// </summary>
    /// <param name="property">The property yielding the objects</param>
    /// <returns><c>true</c> if objects from this source should be disposed; otherwise, <c>false</c></returns>
    bool IsPropertyValueDisposed(PropertyInfo property);

    /// <summary>
    /// Creates an observable expression using a specified lambda expression and arguments
    /// </summary>
    /// <typeparam name="TResult">The type that <paramref name="lambdaExpression"/> returns</typeparam>
    /// <param name="lambdaExpression">The lambda expression</param>
    /// <param name="arguments">The arguments</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TResult> Observe<TResult>(LambdaExpression lambdaExpression, params object?[] arguments);

    /// <summary>
    /// Creates an observable expression using a specified lambda expression and arguments, skipping optimizations
    /// </summary>
    /// <typeparam name="TResult">The type that <paramref name="lambdaExpression"/> returns</typeparam>
    /// <param name="lambdaExpression">The lambda expression</param>
    /// <param name="arguments">The arguments</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TResult> ObserveWithoutOptimization<TResult>(LambdaExpression lambdaExpression, params object?[] arguments);

    /// <summary>
    /// Creates an observable expression using a specified strongly-typed lambda expression with no arguments
    /// </summary>
    /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
    /// <param name="expression">The strongly-typed lambda expression</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TResult> Observe<TResult>(Expression<Func<TResult>> expression);

    /// <summary>
    /// Creates an observable expression using a specified strongly-typed lambda expression with no arguments, skipping optimizations
    /// </summary>
    /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
    /// <param name="expression">The strongly-typed lambda expression</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TResult> ObserveWithoutOptimization<TResult>(Expression<Func<TResult>> expression);

    /// <summary>
    /// Creates an observable expression using a specified strongly-typed lambda expression and one argument
    /// </summary>
    /// <typeparam name="TArgument">The type of the argument</typeparam>
    /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
    /// <param name="argument">The argument</param>
    /// <param name="expression">The strongly-typed lambda expression</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TArgument, TResult> Observe<TArgument, TResult>(Expression<Func<TArgument, TResult>> expression, TArgument argument);

    /// <summary>
    /// Creates an observable expression using a specified strongly-typed lambda expression and one argument, skipping optimizations
    /// </summary>
    /// <typeparam name="TArgument">The type of the argument</typeparam>
    /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
    /// <param name="argument">The argument</param>
    /// <param name="expression">The strongly-typed lambda expression</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TArgument, TResult> ObserveWithoutOptimization<TArgument, TResult>(Expression<Func<TArgument, TResult>> expression, TArgument argument);

    /// <summary>
    /// Creates an observable expression using a specified strongly-typed lambda expression and two arguments
    /// </summary>
    /// <typeparam name="TArgument1">The type of the first argument</typeparam>
    /// <typeparam name="TArgument2">The type of the second argument</typeparam>
    /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
    /// <param name="argument1">The first argument</param>
    /// <param name="argument2">The second argument</param>
    /// <param name="expression">The strongly-typed lambda expression</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TArgument1, TArgument2, TResult> Observe<TArgument1, TArgument2, TResult>(Expression<Func<TArgument1, TArgument2, TResult>> expression, TArgument1 argument1, TArgument2 argument2);

    /// <summary>
    /// Creates an observable expression using a specified strongly-typed lambda expression and two arguments, skipping optimizations
    /// </summary>
    /// <typeparam name="TArgument1">The type of the first argument</typeparam>
    /// <typeparam name="TArgument2">The type of the second argument</typeparam>
    /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
    /// <param name="argument1">The first argument</param>
    /// <param name="argument2">The second argument</param>
    /// <param name="expression">The strongly-typed lambda expression</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TArgument1, TArgument2, TResult> ObserveWithoutOptimization<TArgument1, TArgument2, TResult>(Expression<Func<TArgument1, TArgument2, TResult>> expression, TArgument1 argument1, TArgument2 argument2);

    /// <summary>
    /// Creates an observable expression using a specified strongly-typed lambda expression and three arguments
    /// </summary>
    /// <typeparam name="TArgument1">The type of the first argument</typeparam>
    /// <typeparam name="TArgument2">The type of the second argument</typeparam>
    /// <typeparam name="TArgument3">The type of the third argument</typeparam>
    /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
    /// <param name="argument1">The first argument</param>
    /// <param name="argument2">The second argument</param>
    /// <param name="argument3">The third argument</param>
    /// <param name="expression">The strongly-typed lambda expression</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TArgument1, TArgument2, TArgument3, TResult> Observe<TArgument1, TArgument2, TArgument3, TResult>(Expression<Func<TArgument1, TArgument2, TArgument3, TResult>> expression, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3);

    /// <summary>
    /// Creates an observable expression using a specified strongly-typed lambda expression and three arguments, skipping optimizations
    /// </summary>
    /// <typeparam name="TArgument1">The type of the first argument</typeparam>
    /// <typeparam name="TArgument2">The type of the second argument</typeparam>
    /// <typeparam name="TArgument3">The type of the third argument</typeparam>
    /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
    /// <param name="argument1">The first argument</param>
    /// <param name="argument2">The second argument</param>
    /// <param name="argument3">The third argument</param>
    /// <param name="expression">The strongly-typed lambda expression</param>
    [return: DisposeWhenDiscarded]
    IObservableExpression<TArgument1, TArgument2, TArgument3, TResult> ObserveWithoutOptimization<TArgument1, TArgument2, TArgument3, TResult>(Expression<Func<TArgument1, TArgument2, TArgument3, TResult>> expression, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3);
}
