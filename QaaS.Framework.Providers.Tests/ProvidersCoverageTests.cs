using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QaaS.Framework.Providers.CustomExceptions;
using QaaS.Framework.Providers.Modules;
using QaaS.Framework.Providers.ObjectCreation;
using QaaS.Framework.Providers.Providers;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Hooks;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;

namespace QaaS.Framework.Providers.Tests;

[TestFixture]
public class ProvidersCoverageTests
{
    private sealed class ModuleHook : IHook
    {
        public Context Context { get; set; } = null!;

        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => Scope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class Scope : IDisposable
        {
            public static Scope Instance { get; } = new();
            public void Dispose()
            {
            }
        }
    }

    private static Context CreateContext(ILogger? logger = null) => new()
    {
        Logger = logger ?? NullLogger.Instance,
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
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule($"{assemblyName}.dll");
        var typeBuilder = moduleBuilder.DefineType(
            fullTypeName,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);

        typeBuilder.AddInterfaceImplementation(typeof(IHook));

        var contextField = typeBuilder.DefineField("_context", typeof(Context), FieldAttributes.Private);
        var contextProperty = typeBuilder.DefineProperty(nameof(IHook.Context), PropertyAttributes.None, typeof(Context), null);

        var getContextMethod = typeBuilder.DefineMethod(
            $"get_{nameof(IHook.Context)}",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(Context),
            Type.EmptyTypes);
        var getIl = getContextMethod.GetILGenerator();
        getIl.Emit(OpCodes.Ldarg_0);
        getIl.Emit(OpCodes.Ldfld, contextField);
        getIl.Emit(OpCodes.Ret);

        var setContextMethod = typeBuilder.DefineMethod(
            $"set_{nameof(IHook.Context)}",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null,
            [typeof(Context)]);
        var setIl = setContextMethod.GetILGenerator();
        setIl.Emit(OpCodes.Ldarg_0);
        setIl.Emit(OpCodes.Ldarg_1);
        setIl.Emit(OpCodes.Stfld, contextField);
        setIl.Emit(OpCodes.Ret);

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
        typeBuilder.DefineMethodOverride(loadMethod, typeof(IHook).GetMethod(nameof(IHook.LoadAndValidateConfiguration))!);

        return typeBuilder.CreateType()!;
    }

    [Test]
    public void UnsupportedSubClassException_FormatsExpectedMessage()
    {
        var exception = new UnsupportedSubClassException("MissingHook", typeof(IHook));

        Assert.That(exception.Message, Is.EqualTo("MissingHook not a supported IHook"));
    }

    [Test]
    public void HookProvider_WhenDuplicateSimpleNamesExistAcrossAssemblies_ResolvesFirstAssemblyAndLogsInformation()
    {
        var logger = new RecordingLogger();
        var provider = new HookProvider<IHook>(CreateContext(logger), new ByNameObjectCreator(NullLogger.Instance));
        var firstType = CreateDynamicHookType("GeneratedHooks1", "Hooks.One.SharedHook");
        var secondType = CreateDynamicHookType("GeneratedHooks2", "Hooks.Two.SharedHook");

        OverrideProviderDiscoveryState(provider, [firstType.Assembly, secondType.Assembly], [firstType, secondType]);

        var resolvedHook = provider.GetSupportedInstanceByName("SharedHook");

        Assert.Multiple(() =>
        {
            Assert.That(resolvedHook.GetType().Assembly, Is.EqualTo(firstType.Assembly));
            Assert.That(logger.Entries.Any(entry =>
                entry.Level == LogLevel.Information &&
                entry.Message.Contains("Found multiple IHook hook instances named SharedHook") &&
                entry.Message.Contains(firstType.Assembly.FullName!) &&
                entry.Message.Contains(secondType.Assembly.FullName!)), Is.True);
        });
    }

    [Test]
    public void HookProvider_WhenHookIsMissing_ThrowsAndIncludesDiscoveredAssemblies()
    {
        var provider = new HookProvider<IHook>(CreateContext(), new ByNameObjectCreator(NullLogger.Instance));
        var supportedType = CreateDynamicHookType("GeneratedHooks3", "Hooks.Three.SharedHook");

        OverrideProviderDiscoveryState(provider, [supportedType.Assembly], [supportedType]);

        var exception = Assert.Throws<ArgumentException>(() => provider.GetSupportedInstanceByName("MissingHook"));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("MissingHook"));
            Assert.That(exception.Message, Does.Contain(supportedType.Assembly.FullName));
        });
    }

    [Test]
    public void HookProvider_WhenDuplicateSimpleNamesExistInSingleAssembly_Throws()
    {
        var provider = new HookProvider<IHook>(CreateContext(), new ByNameObjectCreator(NullLogger.Instance));
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("GeneratedHooks4"), AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("GeneratedHooks4.dll");

        Type CreateType(string fullTypeName)
        {
            var typeBuilder = moduleBuilder.DefineType(
                fullTypeName,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
            typeBuilder.AddInterfaceImplementation(typeof(IHook));

            var contextField = typeBuilder.DefineField("_context", typeof(Context), FieldAttributes.Private);
            var contextProperty = typeBuilder.DefineProperty(nameof(IHook.Context), PropertyAttributes.None, typeof(Context), null);

            var getContextMethod = typeBuilder.DefineMethod(
                $"get_{nameof(IHook.Context)}",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                typeof(Context),
                Type.EmptyTypes);
            var getIl = getContextMethod.GetILGenerator();
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, contextField);
            getIl.Emit(OpCodes.Ret);

            var setContextMethod = typeBuilder.DefineMethod(
                $"set_{nameof(IHook.Context)}",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null,
                [typeof(Context)]);
            var setIl = setContextMethod.GetILGenerator();
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, contextField);
            setIl.Emit(OpCodes.Ret);

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
            typeBuilder.DefineMethodOverride(loadMethod, typeof(IHook).GetMethod(nameof(IHook.LoadAndValidateConfiguration))!);

            return typeBuilder.CreateType()!;
        }

        var firstType = CreateType("Hooks.Four.DuplicateHook");
        var secondType = CreateType("Hooks.Other.DuplicateHook");

        OverrideProviderDiscoveryState(provider, [assemblyBuilder], [firstType, secondType]);

        var exception = Assert.Throws<ArgumentException>(() => provider.GetSupportedInstanceByName("DuplicateHook"));

        Assert.That(exception!.Message, Does.Contain("Use the hook's full type name instead."));
    }

    [Test]
    public void ByNameObjectCreator_WhenFullNameMatchesAcrossAssemblies_ThrowsAmbiguousMatch()
    {
        var creator = new ByNameObjectCreator(NullLogger.Instance);
        var firstType = CreateDynamicHookType("GeneratedHooks5", "Hooks.Shared.ExactHook");
        var secondType = CreateDynamicHookType("GeneratedHooks6", "Hooks.Shared.ExactHook");

        var exception = Assert.Throws<AmbiguousMatchException>(() =>
            creator.GetInstanceOfSubClassOfTByNameFromAssemblies<IHook>(
                "Hooks.Shared.ExactHook",
                [firstType.Assembly, secondType.Assembly]));

        Assert.That(exception!.Message, Does.Contain("Hooks.Shared.ExactHook"));
    }

    [Test]
    public void ByNameObjectCreator_HandlesReflectionTypeLoadException_AndSkipsBrokenAssemblies()
    {
        var creator = new ByNameObjectCreator(NullLogger.Instance);
        var partialAssembly = new Mock<Assembly>();
        partialAssembly.Setup(assembly => assembly.GetTypes())
            .Throws(new ReflectionTypeLoadException([typeof(ModuleHook), null], [new Exception("partial")]));
        var brokenAssembly = new Mock<Assembly>();
        brokenAssembly.Setup(assembly => assembly.GetTypes()).Throws(new InvalidOperationException("broken"));

        var instance = creator.GetInstanceOfSubClassOfTByNameFromAssemblies<IHook>(
            nameof(ModuleHook),
            [partialAssembly.Object]);

        Assert.That(instance, Is.InstanceOf<ModuleHook>());
        Assert.Throws<UnsupportedSubClassException>(() =>
            creator.GetInstanceOfSubClassOfTByNameFromAssemblies<IHook>(
                nameof(ModuleHook),
                [brokenAssembly.Object]));
    }

    [Test]
    public void CouldContainHooksCore_DirectQaasReference_IsIncluded()
    {
        var refs = new Dictionary<string, IReadOnlyList<string>?>(StringComparer.OrdinalIgnoreCase)
        {
            ["bin/Plugin.dll"] = new[] { "QaaS.Framework.SDK", "System.Runtime" }
        };

        var include = HookProvider<IHook>.CouldContainHooksCore(
            rootPath: "bin/Plugin.dll",
            rootName: "Plugin",
            dllsByName: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            readReferenceNames: path => refs[path]);

        Assert.That(include, Is.True);
    }

    [Test]
    public void CouldContainHooksCore_TransitiveQaasReferenceViaCorporateBaseLibrary_IsIncluded()
    {
        var dllsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MyCorp.Common.Hooks"] = "bin/MyCorp.Common.Hooks.dll"
        };
        var refs = new Dictionary<string, IReadOnlyList<string>?>(StringComparer.OrdinalIgnoreCase)
        {
            ["bin/Plugin.dll"] = new[] { "MyCorp.Common.Hooks", "System.Runtime" },
            ["bin/MyCorp.Common.Hooks.dll"] = new[] { "QaaS.Framework.SDK" }
        };

        var include = HookProvider<IHook>.CouldContainHooksCore(
            rootPath: "bin/Plugin.dll",
            rootName: "Plugin",
            dllsByName: dllsByName,
            readReferenceNames: path => refs[path]);

        Assert.That(include, Is.True,
            "A plugin DLL that reaches QaaS only through a corporate base library must still be " +
            "discoverable; the direct-reference-only filter that this test guards against would " +
            "silently skip such plugins.");
    }

    [Test]
    public void CouldContainHooksCore_NoQaasInTransitiveClosure_IsExcluded()
    {
        var dllsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ThirdParty.Lib"] = "bin/ThirdParty.Lib.dll"
        };
        var refs = new Dictionary<string, IReadOnlyList<string>?>(StringComparer.OrdinalIgnoreCase)
        {
            ["bin/Unrelated.dll"] = new[] { "ThirdParty.Lib", "System.Runtime" },
            ["bin/ThirdParty.Lib.dll"] = new[] { "System.Runtime" }
        };

        var include = HookProvider<IHook>.CouldContainHooksCore(
            rootPath: "bin/Unrelated.dll",
            rootName: "Unrelated",
            dllsByName: dllsByName,
            readReferenceNames: path => refs[path]);

        Assert.That(include, Is.False);
    }

    [Test]
    public void CouldContainHooksCore_TolerateUnreadableIntermediateButFailRoot()
    {
        var dllsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bad.Lib"] = "bin/Bad.Lib.dll",
            ["Good.Lib"] = "bin/Good.Lib.dll"
        };
        var refs = new Dictionary<string, IReadOnlyList<string>?>(StringComparer.OrdinalIgnoreCase)
        {
            ["bin/Plugin.dll"] = new[] { "Bad.Lib", "Good.Lib" },
            ["bin/Bad.Lib.dll"] = null,
            ["bin/Good.Lib.dll"] = new[] { "QaaS.Framework.SDK" }
        };
        var queriedPaths = new List<string>();

        var include = HookProvider<IHook>.CouldContainHooksCore(
            rootPath: "bin/Plugin.dll",
            rootName: "Plugin",
            dllsByName: dllsByName,
            readReferenceNames: path =>
            {
                queriedPaths.Add(path);
                return refs[path];
            });

        Assert.Multiple(() =>
        {
            Assert.That(include, Is.True,
                "An unreadable intermediate DLL must not poison the walk; the chain through " +
                "Good.Lib still proves the plugin reaches QaaS.");
            Assert.That(queriedPaths, Does.Contain("bin/Bad.Lib.dll"),
                "The unreadable intermediate must have been visited — otherwise the test wouldn't " +
                "be exercising the tolerate-failure path it claims to pin.");
        });
    }

    [Test]
    public void CouldContainHooksCore_RootReadFailure_Excluded()
    {
        var include = HookProvider<IHook>.CouldContainHooksCore(
            rootPath: "bin/Broken.dll",
            rootName: "Broken",
            dllsByName: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            readReferenceNames: _ => null);

        Assert.That(include, Is.False);
    }

    [Test]
    public void HookProvider_GetAssemblyPriority_DoesNotTreatQaasPrefixCollisionAsFrameworkAssembly()
    {
        var priorityMethod = typeof(HookProvider<IHook>)
            .GetMethod("GetAssemblyPriority", BindingFlags.Static | BindingFlags.NonPublic)!;
        var customAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("QaaSMyCustom.Plugin"),
            AssemblyBuilderAccess.Run);

        var priority = (int)priorityMethod.Invoke(null, [customAssembly])!;

        Assert.That(priority, Is.EqualTo(2),
            "Only real framework assembly names that start with 'QaaS.' should receive the QaaS tier; " +
            "a customer assembly such as QaaSMyCustom.Plugin must remain in the normal plugin tier.");
    }

    [Test]
    public void HookProvider_UsesLoadableTypesFromPartiallyLoadedAssemblies()
    {
        var logger = new RecordingLogger();
        var provider = new HookProvider<IHook>(CreateContext(logger), new ByNameObjectCreator(NullLogger.Instance));
        var partialAssembly = new Mock<Assembly>();
        partialAssembly.SetupGet(assembly => assembly.FullName).Returns("GeneratedPartialAssembly");
        partialAssembly.Setup(assembly => assembly.GetTypes())
            .Throws(new ReflectionTypeLoadException([typeof(ModuleHook), null], [new Exception("partial")]));

        typeof(HookProvider<IHook>)
            .GetField("_hookAssemblies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(provider, new[] { partialAssembly.Object });

        var hook = provider.GetSupportedInstanceByName(nameof(ModuleHook));

        Assert.Multiple(() =>
        {
            Assert.That(hook, Is.InstanceOf<ModuleHook>());
            Assert.That(logger.Entries.Any(entry =>
                entry.Level == LogLevel.Debug &&
                entry.Message.Contains("Partially loaded assembly")), Is.True);
        });
    }

    [Test]
    public void HookProvider_WhenConcurrentAssemblyProbeFailsAfterSuccessfulProbe_DoesNotOverwriteCachedHookTypes()
    {
        var provider = new HookProvider<IHook>(CreateContext(), new ByNameObjectCreator(NullLogger.Instance));
        var assembly = new Mock<Assembly>();
        var firstGetTypesEntered = new ManualResetEventSlim();
        var bothGetTypesEntered = new CountdownEvent(2);
        var successfulResolutionCompleted = new ManualResetEventSlim();
        var getTypesCalls = 0;

        assembly.SetupGet(candidate => candidate.FullName).Returns("ConcurrentFlakyAssembly");
        assembly.Setup(candidate => candidate.GetTypes()).Returns(() =>
        {
            var callNumber = Interlocked.Increment(ref getTypesCalls);
            bothGetTypesEntered.Signal();

            if (callNumber == 1)
            {
                firstGetTypesEntered.Set();
                bothGetTypesEntered.Wait(TimeSpan.FromSeconds(5));
                return [typeof(ModuleHook)];
            }

            successfulResolutionCompleted.Wait(TimeSpan.FromSeconds(5));
            throw new InvalidOperationException("transient metadata read failure");
        });

        typeof(HookProvider<IHook>)
            .GetField("_hookAssemblies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(provider, new[] { assembly.Object });

        var successfulResolution = Task.Run(() => provider.GetSupportedInstanceByName(nameof(ModuleHook)));
        Assert.That(firstGetTypesEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);

        var failedResolution = Task.Run(() => provider.GetSupportedInstanceByName(nameof(ModuleHook)));
        Assert.That(successfulResolution.Result, Is.InstanceOf<ModuleHook>());
        successfulResolutionCompleted.Set();

        try
        {
            failedResolution.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }

        var hook = provider.GetSupportedInstanceByName(nameof(ModuleHook));

        Assert.That(hook, Is.InstanceOf<ModuleHook>(),
            "A failed concurrent slow-path probe must not overwrite hook types already discovered " +
            "and cached by another thread for the same assembly.");
    }

    [Test]
    public void HookProvider_WhenSimpleNameResolvesInFirstAssembly_StillReturnsFirstHookWhenLaterAssembliesAreBroken()
    {
        var provider = new HookProvider<IHook>(CreateContext(), new ByNameObjectCreator(NullLogger.Instance));
        var brokenAssembly = new Mock<Assembly>();
        brokenAssembly.SetupGet(assembly => assembly.FullName).Returns("BrokenAssembly");
        brokenAssembly.Setup(assembly => assembly.GetTypes())
            .Throws(new InvalidOperationException("should-not-be-visited"));

        typeof(HookProvider<IHook>)
            .GetField("_hookAssemblies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(provider, new[] { typeof(ModuleHook).Assembly, brokenAssembly.Object });

        var hook = provider.GetSupportedInstanceByName(nameof(ModuleHook));

        Assert.That(hook, Is.InstanceOf<ModuleHook>());
        brokenAssembly.Verify(assembly => assembly.GetTypes(), Times.Once);
    }

    [Test]
    public void HookProvider_WhenExactTypeNameTargetsLaterAssembly_UsesLazyFullNameResolution()
    {
        var logger = new RecordingLogger();
        var provider = new HookProvider<IHook>(CreateContext(logger), new ByNameObjectCreator(NullLogger.Instance));
        var firstType = CreateDynamicHookType("GeneratedHooks7", "Hooks.First.SharedHook");
        var secondType = CreateDynamicHookType("GeneratedHooks8", "Hooks.Second.SharedHook");

        typeof(HookProvider<IHook>)
            .GetField("_hookAssemblies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(provider, new[] { firstType.Assembly, secondType.Assembly });

        var hook = provider.GetSupportedInstanceByName(secondType.FullName!);

        Assert.Multiple(() =>
        {
            Assert.That(hook.GetType(), Is.EqualTo(secondType));
            Assert.That(hook.Context, Is.Not.Null);
            Assert.That(logger.Entries.Any(entry =>
                entry.Level == LogLevel.Information &&
                entry.Message.Contains("appears first in hook discovery order", StringComparison.Ordinal)), Is.False);
        });
    }

    [Test]
    public void ByNameObjectCreator_DefaultAssemblyLookupAndNullAssemblyEnumeration_AreCovered()
    {
        var creator = new ByNameObjectCreator(NullLogger.Instance);
        var getAllSubClasses = typeof(ByNameObjectCreator)
            .GetMethod("GetAllSubClassesOfT", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var discovered = ((IEnumerable<Type>)getAllSubClasses.MakeGenericMethod(typeof(ModuleHook))
            .Invoke(creator, [null])!).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(discovered, Does.Contain(typeof(ModuleHook)));
            Assert.Throws<UnsupportedSubClassException>(() =>
                creator.GetInstanceOfSubClassOfTByNameFromAssemblies<IHook>(nameof(ModuleHook), null));
        });
    }

    [Test]
    public void HooksLoaderModule_RegistersAndResolvesHookCollection()
    {
        var validationResults = new List<ValidationResult>();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(CreateContext()).As<Context>();
        builder.RegisterInstance(new ByNameObjectCreator(NullLogger.Instance)).As<IByNameObjectCreator>();
        builder.RegisterInstance<IEnumerable<HookData<IHook>>>(
        [
            new HookData<IHook>
            {
                Name = "hook-a",
                Type = nameof(ModuleHook),
                Configuration = new ConfigurationBuilder().Build()
            }
        ]);
        builder.RegisterModule(new HooksLoaderModule<IHook>(validationResults));

        using var container = builder.Build();
        var hooks = container.Resolve<IList<KeyValuePair<string, IHook>>>();

        Assert.Multiple(() =>
        {
            Assert.That(hooks, Has.Count.EqualTo(1));
            Assert.That(hooks[0].Key, Is.EqualTo("hook-a"));
            Assert.That(hooks[0].Value, Is.InstanceOf<ModuleHook>());
            Assert.That(validationResults, Is.Empty);
        });
    }
}
