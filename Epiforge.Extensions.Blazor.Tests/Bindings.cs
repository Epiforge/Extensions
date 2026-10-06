namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class Bindings
{
    [TestMethod]
    public void AConvertedBindingConvertsBackWhenWritten()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Age = 36 };
        var conversions = 0;
        var reversals = 0;
        var binding = manager.Binding(() => person.Age, age => { ++conversions; return age.ToString(); }, text => { ++reversals; return int.Parse(text); });
        binding.Value = "85";
        Assert.AreEqual(85, person.Age);
        Assert.AreEqual(0, conversions);
        Assert.AreEqual(1, reversals);
    }

    [TestMethod]
    public void AConvertedBindingConvertsWhenRead()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Age = 36 };
        var conversions = 0;
        var reversals = 0;
        var binding = manager.Binding(() => person.Age, age => { ++conversions; return age.ToString(); }, text => { ++reversals; return int.Parse(text); });
        Assert.AreEqual("36", binding.Value);
        Assert.AreEqual(1, conversions);
        Assert.AreEqual(0, reversals);
    }

    [TestMethod]
    public void AConvertedBindingSubscribes()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Age = 36 } };
        manager.Binding(() => person.Partner.Age, age => age, age => age);
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        person.Partner.Age = 85;
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void BindingReadsTheCurrentValue()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        var binding = manager.Binding(() => person.Name);
        Assert.AreEqual("Ada", binding.Value);
        person.Name = "Grace";
        Assert.AreEqual("Grace", binding.Value);
    }

    [TestMethod]
    public void BindingSubscribes()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Name = "Ada" } };
        manager.Binding(() => person.Partner.Name);
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        Assert.AreEqual(1, person.Partner.PropertyChangedSubscribers);
        person.Partner.Name = "Grace";
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void BindingWritesAField()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person();
        manager.Binding(() => person.Nickname).Value = "Countess";
        Assert.AreEqual("Countess", person.Nickname);
    }

    [TestMethod]
    public void BindingWritesTheValue()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Name = "Ada" } };
        manager.Binding(() => person.Partner.Name).Value = "Grace";
        Assert.AreEqual("Grace", person.Partner.Name);
    }

    [TestMethod]
    public void WritingAConstantIsRefused()
    {
        var (manager, _) = Managers.CreateInitialized();
        var name = "Ada";
        var binding = manager.Binding(() => name);
        Assert.AreEqual("Ada", binding.Value);
        Assert.ThrowsException<InvalidOperationException>(() => binding.Value = "Grace");
    }

    [TestMethod]
    public void WritingAReadOnlyPropertyIsRefused()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Age = 36 };
        var binding = manager.Binding(() => person.ReadOnlyAge);
        Assert.AreEqual(36, binding.Value);
        Assert.ThrowsException<InvalidOperationException>(() => binding.Value = 85);
    }
}
