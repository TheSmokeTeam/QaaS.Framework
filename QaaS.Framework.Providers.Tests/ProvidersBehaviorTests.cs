using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QaaS.Framework.Providers.CustomExceptions;
using QaaS.Framework.Providers.ObjectCreation;
using QaaS.Framework.Providers.Providers;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Hooks;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;

namespace QaaS.Framework.Providers.Tests;

[TestFixture]
public class ProvidersBehaviorTests
{
    private sealed class TestHook : IHook
    {
        public Context Context { get; set; } = null!;

        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration)
            => [];
    }

    private sealed class NotAHook
    {
    }

    private static Context CreateContext() => new()
    {
        Logger = NullLogger.Instance,
        RootConfiguration = new ConfigurationBuilder().Build(),
        CurrentRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
    };

    [Test]
    public void ByNameObjectCreator_IsTypeSubClassOfT_ReturnsExpectedValues()
    {
        var creator = new ByNameObjectCreator(NullLogger.Instance);

        Assert.IsTrue(creator.IsTypeSubClassOfT<IHook>(typeof(TestHook)));
        Assert.IsFalse(creator.IsTypeSubClassOfT<IHook>(typeof(NotAHook)));
    }

    [Test]
    public void ByNameObjectCreator_GetInstanceByName_ReturnsInstance()
    {
        var creator = new ByNameObjectCreator(NullLogger.Instance);

        var instance = creator.GetInstanceOfSubClassOfTByNameFromAssemblies<IHook>(
            nameof(TestHook),
            new[] { Assembly.GetExecutingAssembly() });

        Assert.That(instance, Is.InstanceOf<TestHook>());
    }

    [Test]
    public void ByNameObjectCreator_GetInstanceByName_ThrowsWhenTypeMissing()
    {
        var creator = new ByNameObjectCreator(NullLogger.Instance);

        Assert.Throws<UnsupportedSubClassException>(() =>
            creator.GetInstanceOfSubClassOfTByNameFromAssemblies<IHook>(
                "DoesNotExist",
                new[] { Assembly.GetExecutingAssembly() }));
    }

    [Test]
    public void HookProvider_GetSupportedInstanceByName_ReturnsConfiguredHook()
    {
        var context = CreateContext();
        var provider = new HookProvider<IHook>(context, new ByNameObjectCreator(NullLogger.Instance));

        var instance = provider.GetSupportedInstanceByName(nameof(TestHook));

        Assert.That(instance.GetType().Name, Is.EqualTo(nameof(TestHook)));
        Assert.That(instance.Context, Is.SameAs(context));
    }

    [Test]
    public void HookData_AssignsMetadata()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["k"] = "v"
        }).Build();

        var hookData = new HookData<IHook>
        {
            Name = "my-hook",
            Type = "test",
            Configuration = configuration
        };

        Assert.That(hookData.Name, Is.EqualTo("my-hook"));
        Assert.That(hookData.Type, Is.EqualTo("test"));
        Assert.That(hookData.Configuration["k"], Is.EqualTo("v"));
    }
}
