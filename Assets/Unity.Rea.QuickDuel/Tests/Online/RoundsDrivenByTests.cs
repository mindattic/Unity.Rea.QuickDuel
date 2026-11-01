using NUnit.Framework;
using Unity.Rea.QuickDuel;

// Outside any namespace, so it covers every fixture in this assembly: the Duel scenes these tests load have their rounds
// begun by the tests themselves (controller.BeginRound, a strike key, the online host), so a scene's startup does not
// begin one by itself (DuelStartup, UQD-T66). DuelStartupTests turns it back on for the scenes it loads.
[SetUpFixture]
public class RoundsDrivenByTests
{
    [OneTimeSetUp]
    public void StopTheStartupRunningByItself() => DuelStartup.RunsByItself = false;

    [OneTimeTearDown]
    public void LetItRunAgain() => DuelStartup.RunsByItself = true;
}
