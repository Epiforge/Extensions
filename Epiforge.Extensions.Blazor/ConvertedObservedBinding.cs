namespace Epiforge.Extensions.Blazor;

sealed class ConvertedObservedBinding<TSource, TTarget>(IObservedBinding<TSource> binding, Func<TSource, TTarget> converter, Func<TTarget, TSource> reverseConverter) :
    IObservedBinding<TTarget>
{
    public TTarget Value
    {
        get => converter(binding.Value);
        set => binding.Value = reverseConverter(value);
    }
}
