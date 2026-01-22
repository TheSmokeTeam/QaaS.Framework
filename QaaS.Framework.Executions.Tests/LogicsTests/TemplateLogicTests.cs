// using Microsoft.Extensions.Configuration;
// using Moq;
// using QaaS.Framework.Configurations;
// using QaaS.Framework.Executions.Logics;
// using QaaS.Framework.SDK.ExecutionObjects;
//
// namespace QaaS.Framework.Executions.Tests.LogicsTests;
//
// public class TemplateLogicTests
// {
//     [Test]
//     public void TemplateLogic_ShouldRun_ReturnsTrueOnlyForTemplate()
//     {
//         // Arrange
//         var mockConfig = new Mock<IConfiguration>();
//         var templateLogic = new TemplateLogic(mockConfig.Object);
//         Assert.Multiple(() =>
//         {
//             // Act & Assert
//             Assert.That(templateLogic.ShouldRun(ExecutionType.Template), Is.True);
//             Assert.That(templateLogic.ShouldRun(ExecutionType.Act), Is.False);
//             Assert.That(templateLogic.ShouldRun(ExecutionType.Run), Is.False);
//             Assert.That(templateLogic.ShouldRun(ExecutionType.Assert), Is.False);
//         });
//     }
//
//     // [Test]
//     // public void TemplateLogic_Run_WritesTemplateToTextWriter()
//     // {
//     //     // Arrange
//     //     var mockConfig = new Mock<IConfiguration>();
//     //     var mockWriter = new Mock<TextWriter>();
//     //     var templateLogic = new TemplateLogic(mockConfig.Object, mockWriter.Object);
//     //     var runData = new ExecutionData();
//     //
//     //     mockConfig.Setup(c => c.BuildConfigurationAsYaml(It.IsAny<List<string>>()))
//     //         .Verifiable();//("template content");
//     //
//     //     // Act
//     //     templateLogic.Run(runData);
//     //
//     //     // Assert
//     //     mockWriter.Verify(w => w.WriteLine("template content"), Times.Once);
//     // }
// }