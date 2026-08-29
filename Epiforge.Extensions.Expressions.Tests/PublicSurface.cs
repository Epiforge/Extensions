namespace Epiforge.Extensions.Expressions.Tests;

[TestClass]
public class PublicSurface
{
    [TestMethod]
    public void NoEventIsImplementedAsANoOp()
    {
        var offenders = new List<string>();
        foreach (var type in typeof(IObservableExpression<>).Assembly.GetTypes())
            foreach (var eventInfo in type.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                foreach (var accessor in new MethodInfo?[] { eventInfo.GetAddMethod(true), eventInfo.GetRemoveMethod(true) })
                    if (accessor?.GetMethodBody()?.GetILAsByteArray() is { } il && il.All(instruction => instruction is 0x00 or 0x2a))
                        offenders.Add($"{type.FullName}.{eventInfo.Name} ({accessor.Name})");
        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }
}
