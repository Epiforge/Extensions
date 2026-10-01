#if IS_NET_8_0_OR_GREATER
namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// A lambda compiled into closures, which evaluates its body against a frame of slots for its parameters and variables
/// </summary>
/// <remarks>
/// Each thread keeps one frame for reuse, large enough for the largest lambda it has evaluated, so that evaluations which do not overlap allocate none; an evaluation which finds its thread's frame taken, because another is under way further up the same stack, allocates its own
/// </remarks>
sealed class ClosureLambda(ClosureNode body, int slotCount)
{
    [ThreadStatic]
    static ClosureFrame? spare;

    object? Evaluate(ClosureFrame frame)
    {
        try
        {
            return body.Evaluate(frame);
        }
        finally
        {
            var slots = frame.Slots;
            for (var i = 0; i < slotCount; ++i)
                slots[i].Value = null;
            frame.Caught = null;
            spare = frame;
        }
    }

    ClosureFrame Rent()
    {
        var frame = spare;
        if (frame is null || frame.Slots.Length < slotCount)
            return new ClosureFrame(slotCount);
        spare = null;
        return frame;
    }

    internal object? Invoke(object? argument)
    {
        var frame = Rent();
        frame.Slots[0].Value = argument;
        return Evaluate(frame);
    }

    internal object? Invoke(object? argument0, object? argument1)
    {
        var frame = Rent();
        frame.Slots[0].Value = argument0;
        frame.Slots[1].Value = argument1;
        return Evaluate(frame);
    }

    internal object? Invoke(object? argument0, object? argument1, object? argument2, object? argument3, object? argument4)
    {
        var frame = Rent();
        var slots = frame.Slots;
        slots[0].Value = argument0;
        slots[1].Value = argument1;
        slots[2].Value = argument2;
        slots[3].Value = argument3;
        slots[4].Value = argument4;
        return Evaluate(frame);
    }
}

sealed class ClosureFrame(int slotCount)
{
    internal Exception? Caught;
    internal readonly ClosureSlot[] Slots = new ClosureSlot[slotCount];
}

/// <summary>
/// Holds the value of a parameter or a variable, in a structure so that storing into an array of them is not checked for array covariance as storing into an array of objects is
/// </summary>
struct ClosureSlot
{
    internal object? Value;
}

/// <summary>
/// Evaluates one node of a lambda compiled into closures
/// </summary>
abstract class ClosureNode
{
    internal abstract object? Evaluate(ClosureFrame frame);
}

/// <summary>
/// Casts a boxed value to a type as a cast from <see cref="object"/> does in compiled code: a value type takes a value of exactly its type or of an enumeration or primitive with the same underlying type, and refuses <see langword="null"/> with a <see cref="NullReferenceException"/>; a nullable value type takes <see langword="null"/> or exactly its underlying type; any other type takes <see langword="null"/> or an instance of itself
/// </summary>
sealed class BoxCast
{
    internal BoxCast(Type type)
    {
        this.type = type;
        nullableOf = Nullable.GetUnderlyingType(type);
        if (type.IsValueType && nullableOf is null)
            underlying = UnderlyingOf(type);
    }

    readonly Type? nullableOf;
    readonly Type type;
    readonly Type? underlying;

    internal object? Apply(object? value)
    {
        if (value is null)
            return underlying is null ? null : throw new NullReferenceException();
        var valueType = value.GetType();
        if (valueType == type)
            return value;
        if (nullableOf is not null)
            return valueType == nullableOf ? value : throw Refusal(valueType);
        if (underlying is null)
            return type.IsInstanceOfType(value) ? value : throw Refusal(valueType);
        if (UnderlyingOf(valueType) == underlying)
            return type.IsEnum ? Enum.ToObject(type, value) : Convert.ChangeType(value, type, null);
        throw Refusal(valueType);
    }

    InvalidCastException Refusal(Type valueType) =>
        new($"Unable to cast object of type '{valueType}' to type '{type}'.");

    static Type UnderlyingOf(Type type) =>
        type.IsEnum ? Enum.GetUnderlyingType(type) : type;
}

sealed class ArrayIndexNode(ClosureNode array, ClosureNode index) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        var arrayValue = array.Evaluate(frame);
        var indexValue = (int)index.Evaluate(frame)!;
        return arrayValue is object?[] objects ? objects[indexValue] : ((Array)arrayValue!).GetValue(indexValue);
    }
}

sealed class ArrayLengthNode(ClosureNode array) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        ((Array)array.Evaluate(frame)!).Length;
}

sealed class AssignElementNode(ClosureNode array, ClosureNode index, ClosureNode value) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        var arrayValue = array.Evaluate(frame);
        var indexValue = (int)index.Evaluate(frame)!;
        var assigned = value.Evaluate(frame);
        if (arrayValue is object?[] objects && objects.GetType() == typeof(object[]))
            objects[indexValue] = assigned;
        else
            ((Array)arrayValue!).SetValue(assigned, indexValue);
        return assigned;
    }
}

sealed class AssignSlotNode(int slot, ClosureNode value) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        frame.Slots[slot].Value = value.Evaluate(frame);
}

sealed class BlockNode(int[] variableSlots, object?[] variableDefaults, ClosureNode[] expressions, bool isVoid) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        for (var i = 0; i < variableSlots.Length; ++i)
            frame.Slots[variableSlots[i]].Value = variableDefaults[i];
        object? result = null;
        for (var i = 0; i < expressions.Length; ++i)
            result = expressions[i].Evaluate(frame);
        return isVoid ? null : result;
    }
}

sealed class CastNode(ClosureNode operand, BoxCast cast) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        cast.Apply(operand.Evaluate(frame));
}

sealed class CoalesceNode(ClosureNode left, ClosureNode right) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        left.Evaluate(frame) ?? right.Evaluate(frame);
}

sealed class ConditionalNode(ClosureNode test, ClosureNode ifTrue, ClosureNode ifFalse) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        (bool)test.Evaluate(frame)! ? ifTrue.Evaluate(frame) : ifFalse.Evaluate(frame);
}

sealed class ConstantNode(object? value) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        value;
}

sealed class ConstructionNode(ConstructorInvoker invoker, ClosureNode[] arguments) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        arguments.Length switch
        {
            0 => invoker.Invoke(),
            1 => invoker.Invoke(arguments[0].Evaluate(frame)),
            2 => invoker.Invoke(arguments[0].Evaluate(frame), arguments[1].Evaluate(frame)),
            3 => invoker.Invoke(arguments[0].Evaluate(frame), arguments[1].Evaluate(frame), arguments[2].Evaluate(frame)),
            _ => invoker.Invoke(InvocationNode.EvaluateAll(arguments, frame))
        };
}

sealed class FieldNode(ClosureNode? instance, FieldInfo field) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        if (instance is null)
            return field.GetValue(null);
        return field.GetValue(instance.Evaluate(frame) ?? throw new NullReferenceException());
    }
}

sealed class HasValueNode(ClosureNode nullable) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        BooleanBoxes.Box(nullable.Evaluate(frame) is not null);
}

sealed class InlinedInvocationNode(int[] parameterSlots, ClosureNode[] arguments, ClosureNode body) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        for (var i = 0; i < parameterSlots.Length; ++i)
            frame.Slots[parameterSlots[i]].Value = arguments[i].Evaluate(frame);
        return body.Evaluate(frame);
    }
}

sealed class InvocationNode(ClosureNode? instance, MethodInvoker invoker, ClosureNode[] arguments) :
    ClosureNode
{
    internal static object?[] EvaluateAll(ClosureNode[] nodes, ClosureFrame frame)
    {
        var values = new object?[nodes.Length];
        for (var i = 0; i < values.Length; ++i)
            values[i] = nodes[i].Evaluate(frame);
        return values;
    }

    internal override object? Evaluate(ClosureFrame frame)
    {
        object? target = null;
        if (instance is not null)
            target = instance.Evaluate(frame) ?? throw new NullReferenceException();
        return arguments.Length switch
        {
            0 => invoker.Invoke(target),
            1 => invoker.Invoke(target, arguments[0].Evaluate(frame)),
            2 => invoker.Invoke(target, arguments[0].Evaluate(frame), arguments[1].Evaluate(frame)),
            3 => invoker.Invoke(target, arguments[0].Evaluate(frame), arguments[1].Evaluate(frame), arguments[2].Evaluate(frame)),
            _ => invoker.Invoke(target, EvaluateAll(arguments, frame))
        };
    }
}

sealed class LiftedBinaryNode(ClosureNode left, ClosureNode right, Func<object?, object?, object?> operation, bool lifted, bool comparison, ExpressionType nodeType, bool liftedToNull, bool logical) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        var leftValue = left.Evaluate(frame);
        var rightValue = right.Evaluate(frame);
        if (!lifted || leftValue is not null && rightValue is not null)
            return operation(leftValue, rightValue);
        if (logical)
        {
            var absorbing = nodeType is ExpressionType.Or;
            return leftValue is bool leftBoolean && leftBoolean == absorbing || rightValue is bool rightBoolean && rightBoolean == absorbing ? BooleanBoxes.Box(absorbing) : null;
        }
        if (!comparison || liftedToNull)
            return null;
        return nodeType switch
        {
            ExpressionType.Equal => BooleanBoxes.Box(leftValue is null && rightValue is null),
            ExpressionType.NotEqual => BooleanBoxes.Box(leftValue is not null || rightValue is not null),
            _ => BooleanBoxes.False
        };
    }
}

sealed class LiftedUnaryNode(ClosureNode operand, Func<object?, object?> operation, bool lifted, bool resultNullable) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        var value = operand.Evaluate(frame);
        if (lifted && value is null)
            return resultNullable ? null : throw new InvalidOperationException("Nullable object must have a value.");
        return operation(value);
    }
}

sealed class MemberInitNode(ClosureNode @new, (FieldInfo? Field, MethodInvoker? Setter, ClosureNode Value)[] bindings) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        var instance = @new.Evaluate(frame);
        foreach (var (field, setter, value) in bindings)
            if (field is not null)
                field.SetValue(instance, value.Evaluate(frame));
            else
                setter!.Invoke(instance, value.Evaluate(frame));
        return instance;
    }
}

sealed class NewArrayNode(Type elementType, ClosureNode[] elements) :
    ClosureNode
{
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The array type is the type of a node of the caller's expression, so the caller's code already needed it")]
    internal override object? Evaluate(ClosureFrame frame)
    {
        if (elementType == typeof(object))
            return InvocationNode.EvaluateAll(elements, frame);
        var array = Array.CreateInstance(elementType, elements.Length);
        for (var i = 0; i < elements.Length; ++i)
            array.SetValue(elements[i].Evaluate(frame), i);
        return array;
    }
}

sealed class NullableValueNode(ClosureNode nullable, ClosureNode? defaultValue) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        nullable.Evaluate(frame) ?? defaultValue?.Evaluate(frame) ?? throw new InvalidOperationException("Nullable object must have a value.");
}

sealed class PropertyNode(ClosureNode? instance, PropertyInfo property) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        var target = instance is null ? null : instance.Evaluate(frame) ?? throw new NullReferenceException();
        try
        {
            return property.GetValue(target);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Throw(inner);
            throw;
        }
    }
}

sealed class ReferenceComparisonNode(ClosureNode left, ClosureNode right, Func<object?, object?, object?> operation) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        operation(left.Evaluate(frame), right.Evaluate(frame));
}

sealed class RethrowNode :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        ExceptionDispatchInfo.Throw(frame.Caught!);
        return null;
    }
}

sealed class ShortCircuitNode(ClosureNode left, ClosureNode right, bool and) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        (bool)left.Evaluate(frame)! == and ? right.Evaluate(frame) : BooleanBoxes.Box(!and);
}

sealed class SlotNode(int slot) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        frame.Slots[slot].Value;
}

sealed class ThrowNode(ClosureNode exception) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame) =>
        throw (Exception)exception.Evaluate(frame)!;
}

sealed class TryNode(ClosureNode body, (Type Test, int Slot, ClosureNode Body)[] handlers, ClosureNode? @finally, bool isVoid) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        try
        {
            var result = body.Evaluate(frame);
            return isVoid ? null : result;
        }
        catch (Exception ex) when (HandlerOf(ex) >= 0)
        {
            var (_, slot, handlerBody) = handlers[HandlerOf(ex)];
            if (slot >= 0)
                frame.Slots[slot].Value = ex;
            var enclosing = frame.Caught;
            frame.Caught = ex;
            try
            {
                var result = handlerBody.Evaluate(frame);
                return isVoid ? null : result;
            }
            finally
            {
                frame.Caught = enclosing;
            }
        }
        finally
        {
            @finally?.Evaluate(frame);
        }
    }

    int HandlerOf(Exception exception)
    {
        for (var i = 0; i < handlers.Length; ++i)
            if (handlers[i].Test.IsInstanceOfType(exception))
                return i;
        return -1;
    }
}

sealed class TypeAsNode(ClosureNode operand, Type type) :
    ClosureNode
{
    readonly Type? nullableOf = Nullable.GetUnderlyingType(type);

    internal override object? Evaluate(ClosureFrame frame)
    {
        var value = operand.Evaluate(frame);
        if (value is null)
            return null;
        return nullableOf is not null ? value.GetType() == nullableOf ? value : null : type.IsInstanceOfType(value) ? value : null;
    }
}

sealed class TypeTestNode(ClosureNode operand, Type type, bool exact) :
    ClosureNode
{
    readonly Type tested = Nullable.GetUnderlyingType(type) ?? type;

    internal override object? Evaluate(ClosureFrame frame)
    {
        var value = operand.Evaluate(frame);
        return BooleanBoxes.Box(value is not null && (exact ? value.GetType() == tested : tested.IsInstanceOfType(value)));
    }
}

sealed class ValueConversionNode(ClosureNode operand, Func<object?, object?>? conversion, bool fromNullable, bool toNullable) :
    ClosureNode
{
    internal override object? Evaluate(ClosureFrame frame)
    {
        var value = operand.Evaluate(frame);
        if (value is null)
            return fromNullable && toNullable ? null : throw new InvalidOperationException("Nullable object must have a value.");
        return conversion is null ? value : conversion(value);
    }
}
#endif
