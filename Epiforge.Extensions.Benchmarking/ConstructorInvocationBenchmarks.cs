namespace Epiforge.Extensions.Benchmarking;

using System.Reflection.Emit;

[MemoryDiagnoser]
public class ConstructorInvocationBenchmarks
{
    delegate object InvokeConstructorDelegate(object?[] arguments);

    const int iterations = 1000;

    object?[] arguments = null!;
    InvokeConstructorDelegate compiled = null!;
    ConstructorInfo constructor = null!;
    ConstructorInvoker invoker = null!;

    [Benchmark(Baseline = true)]
    public object ConstructDirectly()
    {
        object result = null!;
        for (var i = 0; i < iterations; ++i)
            result = new KeyValuePair<int, int>(1, 2);
        return result;
    }

    [Benchmark]
    public object? InvokeThroughACompiledDelegate()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = compiled(arguments);
        return result;
    }

    [Benchmark]
    public object? InvokeThroughAConstructorInvoker()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = invoker.Invoke(arguments[0], arguments[1]);
        return result;
    }

    [Benchmark]
    public object? InvokeThroughAConstructorInvokerWithASpan()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = invoker.Invoke(arguments.AsSpan());
        return result;
    }

    [Benchmark]
    public object? InvokeThroughReflection()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = constructor.Invoke(BindingFlags.DoNotWrapExceptions, null, arguments, null);
        return result;
    }

    [GlobalSetup]
    public void Setup()
    {
        constructor = typeof(KeyValuePair<int, int>).GetConstructor([typeof(int), typeof(int)])!;
        arguments = [1, 2];
        invoker = ConstructorInvoker.Create(constructor);
        var declaringType = constructor.DeclaringType!;
        var dynamicMethod = new DynamicMethod($"CreateInstance_{declaringType.Name}", typeof(object), [typeof(object[])]);
        var ilGenerator = dynamicMethod.GetILGenerator();
        var parameters = constructor.GetParameters();
        for (var i = 0; i < parameters.Length; ++i)
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
        compiled = (InvokeConstructorDelegate)dynamicMethod.CreateDelegate(typeof(InvokeConstructorDelegate));
    }
}
