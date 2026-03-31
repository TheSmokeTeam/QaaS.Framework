using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
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

    private static void OverrideProviderDiscoveryState(
        HookProvider<IHook> provider,
        Assembly[] hookAssemblies,
        Type[] supportedHookTypes)
    {
        typeof(HookProvider<IHook>)
            .GetField("_hookAssemblies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(provider, hookAssemblies);
        typeof(HookProvider<IHook>)
            .GetField("_supportedHookTypes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(provider, supportedHookTypes);
    }

    private static Type CreateDynamicHookType(string assemblyName, string fullTypeName)
    {
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule($"{assemblyName}.dll");
        var typeBuilder = moduleBuilder.DefineType(
            fullTypeName,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);

        typeBuilder.AddInterfaceImplementation(typeof(IHook));

        var contextField = typeBuilder.DefineField("_context", typeof(Context), FieldAttributes.Private);

        var contextProperty = typeBuilder.DefineProperty(
            nameof(IHook.Context),
            PropertyAttributes.None,
            typeof(Context),
            null);

        var getContextMethod = typeBuilder.DefineMethod(
            $"get_{nameof(IHook.Context)}",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(Context),
            Type.EmptyTypes);
        var getContextIl = getContextMethod.GetILGenerator();
        getContextIl.Emit(OpCodes.Ldarg_0);
        getContextIl.Emit(OpCodes.Ldfld, contextField);
        getContextIl.Emit(OpCodes.Ret);

        var setContextMethod = typeBuilder.DefineMethod(
            $"set_{nameof(IHook.Context)}",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null,
            [typeof(Context)]);
        var setContextIl = setContextMethod.GetILGenerator();
        setContextIl.Emit(OpCodes.Ldarg_0);
        setContextIl.Emit(OpCodes.Ldarg_1);
        setContextIl.Emit(OpCodes.Stfld, contextField);
        setContextIl.Emit(OpCodes.Ret);

        contextProperty.SetGetMethod(getContextMethod);
        contextProperty.SetSetMethod(setContextMethod);
        typeBuilder.DefineMethodOverride(getContextMethod, typeof(IHook).GetProperty(nameof(IHook.Context))!.GetMethod!);
        typeBuilder.DefineMethodOverride(setContextMethod, typeof(IHook).GetProperty(nameof(IHook.Context))!.SetMethod!);

        var loadMethod = typeBuilder.DefineMethod(
            nameof(IHook.LoadAndValidateConfiguration),
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(List<ValidationResult>),
            [typeof(IConfiguration)]);
        var loadIl = loadMethod.GetILGenerator();
        loadIl.Emit(OpCodes.Newobj, typeof(List<ValidationResult>).GetConstructor(Type.EmptyTypes)!);
        loadIl.Emit(OpCodes.Ret);
        typeBuilder.DefineMethodOverride(
            loadMethod,
            typeof(IHook).GetMethod(nameof(IHook.LoadAndValidateConfiguration))!);

        return typeBuilder.CreateType()!;
    }

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
    public void ByNameObjectCreator_GetInstanceByName_WhenSimpleNameIsAmbiguous_Throws()
    {
        var creator = new ByNameObjectCreator(NullLogger.Instance);

        var exception = Assert.Throws<AmbiguousMatchException>(() =>
            creator.GetInstanceOfSubClassOfTByNameFromAssemblies<IHook>(
                nameof(NamespaceA.DuplicateHook),
                new[] { Assembly.GetExecutingAssembly() }));

        Assert.That(exception!.Message, Does.Contain(typeof(NamespaceA.DuplicateHook).FullName));
        Assert.That(exception.Message, Does.Contain(typeof(NamespaceB.DuplicateHook).FullName));
    }

    [Test]
    public void ByNameObjectCreator_GetInstanceByName_WhenFullNameIsProvided_ReturnsExpectedHook()
    {
        var creator = new ByNameObjectCreator(NullLogger.Instance);

        var instance = creator.GetInstanceOfSubClassOfTByNameFromAssemblies<IHook>(
            typeof(NamespaceB.DuplicateHook).FullName!,
            new[] { Assembly.GetExecutingAssembly() });

        Assert.That(instance, Is.InstanceOf<NamespaceB.DuplicateHook>());
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
    public void HookProvider_GetSupportedInstanceByName_WhenSimpleNameIsAmbiguous_Throws()
    {
        var context = CreateContext();
        var provider = new HookProvider<IHook>(context, new ByNameObjectCreator(NullLogger.Instance));

        var exception = Assert.Throws<ArgumentException>(() =>
            provider.GetSupportedInstanceByName(nameof(NamespaceA.DuplicateHook)));

        Assert.That(exception!.Message, Does.Contain(typeof(NamespaceA.DuplicateHook).FullName));
        Assert.That(exception.Message, Does.Contain(typeof(NamespaceB.DuplicateHook).FullName));
    }

    [Test]
    public void HookProvider_GetSupportedInstanceByName_WhenFullNameIsProvided_ReturnsExpectedHook()
    {
        var context = CreateContext();
        var provider = new HookProvider<IHook>(context, new ByNameObjectCreator(NullLogger.Instance));

        var instance = provider.GetSupportedInstanceByName(typeof(NamespaceA.DuplicateHook).FullName!);

        Assert.That(instance, Is.InstanceOf<NamespaceA.DuplicateHook>());
        Assert.That(instance.Context, Is.SameAs(context));
    }

    [Test]
    public void HookProvider_GetSupportedInstanceByName_WhenFullNameMatchesMultipleAssemblies_Throws()
    {
        var context = CreateContext();
        var provider = new HookProvider<IHook>(context, new ByNameObjectCreator(NullLogger.Instance));
        var duplicateTypeName = "QaaS.Framework.Providers.Tests.Generated.DuplicateHook";
        var firstType = CreateDynamicHookType("GeneratedHooksA", duplicateTypeName);
        var secondType = CreateDynamicHookType("GeneratedHooksB", duplicateTypeName);

        OverrideProviderDiscoveryState(
            provider,
            [firstType.Assembly, secondType.Assembly],
            [firstType, secondType]);

        var exception = Assert.Throws<ArgumentException>(() =>
            provider.GetSupportedInstanceByName(duplicateTypeName));

        Assert.That(exception!.Message, Does.Contain("assembly-qualified name"));
        Assert.That(exception.Message, Does.Contain(firstType.Assembly.FullName));
        Assert.That(exception.Message, Does.Contain(secondType.Assembly.FullName));
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
