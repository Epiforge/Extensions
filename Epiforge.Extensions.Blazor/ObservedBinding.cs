namespace Epiforge.Extensions.Blazor;

sealed class ObservedBinding<T>(LambdaExpression valueAccessor, object? root, MemberPath? path) :
    IObservedBinding<T>
{
    public T Value
    {
        get
        {
            if (path is null)
                return (T)root!;
            var steps = path.Steps;
            var last = steps.Length - 1;
            var target = root ?? throw new NullReferenceException();
            for (var i = 0; i < last; ++i)
                target = steps[i].Read(target) ?? throw new NullReferenceException();
            return steps[last].Read<T>(target);
        }
        set
        {
            if (path is null || !path.IsWritable)
                throw new InvalidOperationException($"Unable to set the value of '{valueAccessor}'. The target is read-only.");
            var steps = path.Steps;
            var last = steps.Length - 1;
            var target = root ?? throw new NullReferenceException();
            for (var i = 0; i < last; ++i)
                target = steps[i].Read(target) ?? throw new NullReferenceException();
            path.Write(target, value);
        }
    }
}
