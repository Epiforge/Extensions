namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class PhorkAgreement
{
    sealed class World
    {
        public Person Ada { get; } = new() { Name = "Ada", Age = 36, Tags = [] };
        public Person Grace { get; } = new() { Name = "Grace", Age = 85, Tags = [] };
        public Person Hedy { get; } = new() { Name = "Hedy", Age = 85, Tags = [] };

        public string Snapshot(IAgreementDriver driver) =>
            $"notifications {driver.Notifications}; subscribers {Ada.PropertyChangedSubscribers} {Grace.PropertyChangedSubscribers} {Hedy.PropertyChangedSubscribers}; collection subscribers {Ada.Tags!.CollectionChangedSubscribers} {Grace.Tags!.CollectionChangedSubscribers} {Hedy.Tags!.CollectionChangedSubscribers}";
    }

    static List<string> Run(IAgreementDriver driver, Action<IAgreementDriver, World, Action<string>> scenario)
    {
        var world = new World();
        var log = new List<string>();
        scenario(driver, world, step =>
        {
            log.Add($"{step}: {world.Snapshot(driver)}");
        });
        return log;
    }

    static void AssertAgreement(Action<IAgreementDriver, World, Action<string>> scenario)
    {
        var phork = Run(new PhorkDriver(), scenario);
        var epiforge = Run(new EpiforgeDriver(), scenario);
        CollectionAssert.AreEqual(phork, epiforge, $"Phork:{Environment.NewLine}{string.Join(Environment.NewLine, phork)}{Environment.NewLine}Epiforge:{Environment.NewLine}{string.Join(Environment.NewLine, epiforge)}");
    }

    static string Attempt(Action action)
    {
        try
        {
            action();
            return "succeeded";
        }
        catch (Exception exception)
        {
            return $"threw {exception.GetType().Name}";
        }
    }

    static string? ReadName(IAgreementDriver driver, Person person) =>
        driver.Observed(() => person.Name);

    [TestMethod]
    public void AChainWhoseMiddleIsReplaced() =>
        AssertAgreement((driver, world, step) =>
        {
            world.Ada.Partner = world.Grace;
            var ada = world.Ada;
            step($"observed {driver.Observed(() => ada.Partner.Name)}");
            world.Grace.Name = "Grace Hopper";
            step("leaf changed");
            ada.Partner = world.Hedy;
            step("middle replaced");
            step($"observed again {driver.Observed(() => ada.Partner.Name)}");
            driver.EndCycle();
            step("cycle ended");
            world.Grace.Name = "Grace";
            step("old leaf changed");
            world.Hedy.Name = "Hedy Lamarr";
            step("new leaf changed");
            driver.EndCycle();
            step("cycle ended unused");
        });

    [TestMethod]
    public void AClosureOnlyExpression() =>
        AssertAgreement((driver, world, step) =>
        {
            var ada = world.Ada;
            step($"observed {driver.Observed(() => ada).Name}");
            ada.Name = "Ada Lovelace";
            step("changed");
        });

    [TestMethod]
    public void ACollectionReplacedAndDropped() =>
        AssertAgreement((driver, world, step) =>
        {
            var ada = world.Ada;
            driver.ObservedCollection(() => ada.Tags);
            step("observed collection");
            ada.Tags!.Add("first");
            step("added");
            var replacement = world.Grace.Tags;
            ada.Tags = replacement;
            step("replaced");
            driver.ObservedCollection(() => ada.Tags);
            driver.EndCycle();
            step("cycle ended");
            driver.Observed(() => ada.Tags);
            driver.EndCycle();
            step("observed only as a value");
            replacement!.Add("second");
            step("added to the replacement");
        });

    [TestMethod]
    public void ANullIntermediate() =>
        AssertAgreement((driver, world, step) =>
        {
            var ada = world.Ada;
            step(Attempt(() => driver.Observed(() => ada.Partner!.Name)));
            ada.Partner = world.Grace;
            step("partner set");
            step($"observed {driver.Observed(() => ada.Partner.Name)}");
            driver.EndCycle();
            ada.Partner = null;
            step(Attempt(() => driver.Observed(() => ada.Partner!.Name)));
            driver.EndCycle();
            step("cycle ended");
            world.Grace.Name = "Grace Hopper";
            step("stranded leaf changed");
        });

    [TestMethod]
    public void ANullPropertyName() =>
        AssertAgreement((driver, world, step) =>
        {
            var ada = world.Ada;
            driver.Observed(() => ada.Name);
            ada.Raise(null);
            step("raised null");
            ada.Raise("Unobserved");
            step("raised unobserved");
        });

    [TestMethod]
    public void AnEmptyPropertyNameIsWhereTheyDisagree()
    {
        static int NotificationsAfterRaisingEmpty(IAgreementDriver driver)
        {
            var ada = new Person { Name = "Ada" };
            driver.Observed(() => ada.Name);
            ada.Raise(string.Empty);
            return driver.Notifications;
        }
        Assert.AreEqual(0, NotificationsAfterRaisingEmpty(new PhorkDriver()));
        Assert.AreEqual(1, NotificationsAfterRaisingEmpty(new EpiforgeDriver()));
    }

    [TestMethod]
    public void ConditionalObservations() =>
        AssertAgreement((driver, world, step) =>
        {
            var ada = world.Ada;
            for (var cycle = 0; cycle < 4; ++cycle)
            {
                driver.Observed(() => ada.Name);
                if (cycle % 2 == 0)
                    driver.Observed(() => ada.Age);
                driver.EndCycle();
                ada.Age += 1;
                step($"cycle {cycle} then age changed");
                ada.Name += "!";
                step($"cycle {cycle} then name changed");
            }
        });

    [TestMethod]
    public void ObservationsOutsideACycle() =>
        AssertAgreement((driver, world, step) =>
        {
            var ada = world.Ada;
            driver.Observed(() => ada.Name);
            driver.EndCycle();
            step("first end");
            ada.Name = "Ada Lovelace";
            step("changed after first end");
            driver.EndCycle();
            step("second end");
            ada.Name = "Ada";
            step("changed after second end");
        });

    [TestMethod]
    public void SharedShapes() =>
        AssertAgreement((driver, world, step) =>
        {
            step($"read {ReadName(driver, world.Ada)} {ReadName(driver, world.Grace)} {ReadName(driver, world.Hedy)} {ReadName(driver, world.Ada)}");
            world.Grace.Name = "Grace Hopper";
            step("grace changed");
            driver.EndCycle();
            step($"read {ReadName(driver, world.Grace)}");
            driver.EndCycle();
            step("cycle ended");
            world.Ada.Name = "Ada Lovelace";
            step("ada changed");
        });
}
