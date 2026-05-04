using System.Reflection;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.SDK.ContextObjects;

namespace QaaS.Framework.SDK.Tests.BuildersTests;

[TestFixture]
public class ContextBuilderCloneTests
{
    private static readonly FieldInfo ConfigurationBuilderField =
        typeof(ContextBuilder).GetField(
            "_configurationBuilder",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Test]
    public void Clone_IsolatesConfigurationBuilder_AcrossBuilds()
    {
        var sharedSourceBuilder = new ConfigurationBuilder();
        var original = new ContextBuilder(sharedSourceBuilder);

        var clone = original.Clone();

        var originalInner = (IConfigurationBuilder)ConfigurationBuilderField.GetValue(original)!;
        var cloneInner = (IConfigurationBuilder)ConfigurationBuilderField.GetValue(clone)!;

        Assert.Multiple(() =>
        {
            Assert.That(cloneInner, Is.Not.SameAs(originalInner),
                "Clone must own a fresh IConfigurationBuilder instance.");
            Assert.That(cloneInner.Sources, Is.Not.SameAs(originalInner.Sources),
                "Clone must own its own Sources list.");
        });

        clone.WithOverwriteArgument("--clone-only=1");
        clone.BuildInternal();

        Assert.That(originalInner.Sources, Is.Empty,
            "Building the clone must not append sources to the original's IConfigurationBuilder.");
    }
}
