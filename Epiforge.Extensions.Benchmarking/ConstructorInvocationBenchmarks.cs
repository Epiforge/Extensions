namespace Epiforge.Extensions.Benchmarking;

using System.Reflection.Emit;

[MemoryDiagnoser]
public class ConstructorInvocationBenchmarks
{
    delegate object InvokeConstructorDelegate(object?[] arguments);

    delegate object InvokeMethodDelegate(object? instance, object?[] arguments);

    const int iterations = 1000;

    object?[] arguments = null!;
    InvokeConstructorDelegate compiled = null!;
    ConstructorInfo constructor = null!;
    ConstructorInvoker invoker = null!;
    MethodInfo method = null!;
    object?[] methodArguments = null!;
    InvokeMethodDelegate methodCompiled = null!;
    MethodInvoker methodInvoker = null!;
    BenchmarkPerson person = null!;
    PropertyInfo rank = null!;
    string text = null!;
    PropertyInfo textLength = null!;

    [Benchmark]
    public object CallDirectly()
    {
        object result = null!;
        for (var i = 0; i < iterations; ++i)
            result = text.IndexOf('m');
        return result;
    }

    [Benchmark]
    public object? CallThroughACompiledDelegate()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = methodCompiled(text, methodArguments);
        return result;
    }

    [Benchmark]
    public object? CallThroughAMethodInvoker()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = methodInvoker.Invoke(text, methodArguments[0]);
        return result;
    }

    [Benchmark]
    public object? CallThroughFastInvoke()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = method.FastInvoke(text, methodArguments);
        return result;
    }

    [Benchmark]
    public object? CallThroughReflection()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = method.Invoke(text, BindingFlags.DoNotWrapExceptions, null, methodArguments, null);
        return result;
    }

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
    public object? InvokeThroughFastInvoke()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = constructor.FastInvoke(arguments);
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

    [Benchmark]
    public object? GetPropertyDirectly()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = text.Length;
        return result;
    }

    [Benchmark]
    public object? GetPropertyThroughFastGetValue()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = textLength.FastGetValue(text);
        return result;
    }

    [Benchmark]
    public object? GetPropertyThroughReflection()
    {
        object? result = null;
        for (var i = 0; i < iterations; ++i)
            result = textLength.GetValue(text);
        return result;
    }

    [Benchmark]
    public void SetPropertyDirectly()
    {
        for (var i = 0; i < iterations; ++i)
            person.Rank = i;
    }

    [Benchmark]
    public void SetPropertyThroughFastSetValue()
    {
        for (var i = 0; i < iterations; ++i)
            rank.FastSetValue(person, i);
    }

    [Benchmark]
    public void SetPropertyThroughReflection()
    {
        for (var i = 0; i < iterations; ++i)
            rank.SetValue(person, i);
    }

    [GlobalSetup]
    public void Setup()
    {
        person = new BenchmarkPerson("P", 0);
        rank = typeof(BenchmarkPerson).GetProperty(nameof(BenchmarkPerson.Rank))!;
        textLength = typeof(string).GetProperty(nameof(string.Length))!;
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
        text = "benchmark";
        method = typeof(string).GetMethod(nameof(string.IndexOf), [typeof(char)])!;
        methodArguments = ['m'];
        methodInvoker = MethodInvoker.Create(method);
        var methodDynamicMethod = new DynamicMethod($"Invoke_{method.Name}", typeof(object), [typeof(object), typeof(object[])]);
        var methodIlGenerator = methodDynamicMethod.GetILGenerator();
        methodIlGenerator.Emit(OpCodes.Ldarg_0);
        methodIlGenerator.Emit(OpCodes.Castclass, method.DeclaringType!);
        var methodParameters = method.GetParameters();
        for (var i = 0; i < methodParameters.Length; ++i)
        {
            methodIlGenerator.Emit(OpCodes.Ldarg_1);
            methodIlGenerator.Emit(OpCodes.Ldc_I4, i);
            methodIlGenerator.Emit(OpCodes.Ldelem_Ref);
            methodIlGenerator.Emit(OpCodes.Unbox_Any, methodParameters[i].ParameterType);
        }
        methodIlGenerator.Emit(OpCodes.Callvirt, method);
        methodIlGenerator.Emit(OpCodes.Box, method.ReturnType);
        methodIlGenerator.Emit(OpCodes.Ret);
        methodCompiled = (InvokeMethodDelegate)methodDynamicMethod.CreateDelegate(typeof(InvokeMethodDelegate));
    }
}
