// using System.Collections.Immutable;
// using QaaS.Framework.Executions.Logics;
// using QaaS.Framework.SDK.DataSourceObjects;
// using QaaS.Framework.SDK.ExecutionObjects;
// using QaaS.Framework.SDK.Session.SessionDataObjects;
//
// namespace QaaS.Framework.Executions.Tests.LogicsTests;
//
// internal class MockSession() : ISession
// {
//     public void Dispose()
//     {
//         throw new NotImplementedException();
//     }
//
//     public string Name { get; set; }
//     public int Stage { get; set; }
//     public int RunUntilStage { get; set; }
//     public int TimeBefore { get; set; }
//     public int TimeAfter { get; set; }
//
//     public SessionData? PerformSession(IImmutableList<DataSource> dataSources,
//         IImmutableList<SessionData> ranSessionDatas)
//     {
//         throw new NotImplementedException();
//     }
// }
//
// public class SessionLogicTests
// {
//     [Test]
//     public void SessionLogic_ShouldRun_ReturnsTrueForActOrRun()
//     {
//         // Arrange
//         var sessionLogic = new SessionLogic(new List<ISession>(), Globals.Logger);
//         Assert.Multiple(() =>
//         {
//
//             // Act & Assert
//             Assert.That(sessionLogic.ShouldRun(ExecutionType.Act), Is.True);
//             Assert.That(sessionLogic.ShouldRun(ExecutionType.Run), Is.True);
//             Assert.That(sessionLogic.ShouldRun(ExecutionType.Assert), Is.False);
//             Assert.That(sessionLogic.ShouldRun(ExecutionType.Template), Is.False);
//         });
//     }
// }