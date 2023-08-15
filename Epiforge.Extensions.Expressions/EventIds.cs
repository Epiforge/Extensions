namespace Epiforge.Extensions.Expressions;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// Provides all the event IDs for the Epiforge.Extensions.Collections namespace
/// </summary>
public static class EventIds
{
    public static readonly EventId Epiforge_Extensions_Expressions_ExpressionInitialized = new(1, nameof(Epiforge_Extensions_Expressions_ExpressionInitialized));
    public static readonly EventId Epiforge_Extensions_Expressions_ExpressionDisposed = new(2, nameof(Epiforge_Extensions_Expressions_ExpressionDisposed));
    public static readonly EventId Epiforge_Extensions_Expressions_ExpressionFaulted = new(3, nameof(Epiforge_Extensions_Expressions_ExpressionFaulted));
    public static readonly EventId Epiforge_Extensions_Expressions_ExpressionEvaluated = new(4, nameof(Epiforge_Extensions_Expressions_ExpressionEvaluated));
    public static readonly EventId Epiforge_Extensions_Expressions_ConditionalExpressionTestInvalidType = new(5, nameof(Epiforge_Extensions_Expressions_ConditionalExpressionTestInvalidType));
    public static readonly EventId Epiforge_Extensions_Expressions_QueryInitialized = new(6, nameof(Epiforge_Extensions_Expressions_QueryInitialized));
    public static readonly EventId Epiforge_Extensions_Expressions_QueryDisposed = new(7, nameof(Epiforge_Extensions_Expressions_QueryDisposed));
    public static readonly EventId Epiforge_Extensions_Expressions_QueryFaulted = new(8, nameof(Epiforge_Extensions_Expressions_QueryFaulted));
    public static readonly EventId Epiforge_Extensions_Expressions_QueryEvaluated = new(9, nameof(Epiforge_Extensions_Expressions_QueryEvaluated));
}
