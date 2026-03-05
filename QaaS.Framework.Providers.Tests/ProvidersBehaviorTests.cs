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

    private sealed class ValidationHook : IHook
    {
        public Context Context { get; set; } = null!;

        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration)
            => [new ValidationResult("invalid configuration")];
    }

    private sealed class StaticHookProvider(IHook hook) : IHookProvider<IHook>
    {
        public IHook GetSupportedInstanceByName(string instanceName) => hook;
    }

    private sealed class ThrowingHookProvider : IHookProvider<IHook>
    {
        public IHook GetSupportedInstanceByName(string instanceName)
            => throw new ArgumentException("missing hook");
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

    [Test]
    public void HooksFromProvidersLoader_LoadAndValidate_PrefixesValidationErrors()
    {
        var context = CreateContext();
        var loader = new HooksFromProvidersLoader<IHook>(context, new StaticHookProvider(new ValidationHook()));
        var validationResults = new List<ValidationResult>();

        var loaded = loader.LoadAndValidate(
            [
                new HookData<IHook>
                {
                    Name = "hook-a",
                    Type = nameof(ValidationHook),
                    Configuration = new ConfigurationBuilder().Build()
                }
            ],
            validationResults);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Has.Count.EqualTo(1));
            Assert.That(validationResults, Has.Count.EqualTo(1));
            Assert.That(validationResults[0].ErrorMessage, Does.Contain("In Hook of IHook named hook-a"));
            Assert.That(validationResults[0].ErrorMessage, Does.Contain(nameof(ValidationHook)));
            Assert.That(validationResults[0].ErrorMessage, Does.Contain("invalid configuration"));
        });
    }

    [Test]
    public void HooksFromProvidersLoader_WhenProviderFails_ThrowsArgumentException()
    {
        var context = CreateContext();
        var loader = new HooksFromProvidersLoader<IHook>(context, new ThrowingHookProvider());
        var validationResults = new List<ValidationResult>();

        Assert.Throws<ArgumentException>(() => loader.LoadAndValidate(
            [
                new HookData<IHook>
                {
                    Name = "hook-a",
                    Type = nameof(TestHook),
                    Configuration = new ConfigurationBuilder().Build()
                }
            ],
            validationResults));
    }
}
