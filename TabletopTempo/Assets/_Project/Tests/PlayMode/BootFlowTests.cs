using System.Collections;
using NUnit.Framework;
using TabletopTempo.Boot;
using TabletopTempo.Core.Platform;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TabletopTempo.Tests
{
    /// <summary>M1 smoke test: Boot → Game, capabilities resolved, quality tier applied.</summary>
    public class BootFlowTests
    {
        [UnityTest]
        public IEnumerator Boot_LoadsGame_WithQuest2Profile()
        {
            // No XR runtime in batch mode: the Meta rig logs errors we don't care about here.
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Boot", LoadSceneMode.Single);

            for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != "Game"; i++)
                yield return null;

            Assert.AreEqual("Game", SceneManager.GetActiveScene().name, "Boot should hand over to Game");
            Assert.IsTrue(CapabilityService.IsInitialized);

            var caps = CapabilityService.Current;
            Assert.AreEqual("editor-simulated-quest2", caps.Source, "editor defaults to the Quest 2 profile");
            Assert.AreEqual(QualityTier.Low, caps.Tier);
            Assert.IsFalse(caps.UseCameraCupFinder);
            Assert.IsFalse(caps.UseDepthRaycast);

            Assert.AreEqual(QualityTierApplier.LowLevelName, QualitySettings.names[QualitySettings.GetQualityLevel()]);
            Assert.IsNotNull(Resources.Load("TMP Settings"), "TMP essentials must be imported (text renders nothing without them)");
            Assert.IsNotNull(GameObject.Find("StatusPanel"), "status card should exist");
            Assert.IsNotNull(GameObject.Find("TableAnchor"), "table anchor should exist");
        }
    }
}
