using System.Collections;
using NUnit.Framework;
using TabletopTempo.Core.Rhythm;
using TabletopTempo.Rhythm;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TabletopTempo.Tests
{
    /// <summary>
    /// Stage 1 end-to-end in the real Game scene: highway, judge, scoring, results. The clock is
    /// manual (fast-forwarded) and autoplay supplies perfectly timed claps, so this runs headless.
    /// </summary>
    public class StageFlowTests
    {
        [UnityTest]
        public IEnumerator Stage1_Autoplay_AllPerfect_ThreeStars()
        {
            LogAssert.ignoreFailingMessages = true; // no XR runtime / audio device in batch mode
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);

            var runner = Object.FindAnyObjectByType<StageRunner>();
            Assert.IsNotNull(runner, "StageRunner in Game scene");
            Assert.IsNotNull(runner.stage.chart, "stage 1 chart assigned");

            var clock = new ManualClock { Now = 100.0 };
            runner.conductor.Clock = clock;
            runner.skipTry = true;
            runner.autoplay = true;
            runner.introSeconds = 0f;
            runner.StartStage();

            int notes = runner.stage.chart.Chart.Notes.Count;
            Assert.AreEqual(48, notes, "stage 1 has 48 claps");

            for (int frame = 0; frame < 20000 && runner.Current != StageRunner.State.Results; frame++)
            {
                clock.Now += 0.02;   // 50 song-seconds per 2500 frames
                yield return null;
            }

            Assert.AreEqual(StageRunner.State.Results, runner.Current, "reached results");
            Assert.AreEqual(notes, runner.Score.Notes, "every note judged once");
            Assert.AreEqual(notes, runner.Score.Counts[(int)Grade.Perfect], "autoplay is perfect");
            Assert.AreEqual(3, runner.Score.Stars);
            Assert.AreEqual(notes, runner.Score.BestCombo);
        }

        [UnityTest]
        public IEnumerator TryPhase_CountsClaps_ThenStartsPlay()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            var runner = Object.FindAnyObjectByType<StageRunner>();
            runner.conductor.Clock = new ManualClock { Now = 50.0 };
            runner.introSeconds = 0f;
            runner.autoplay = false;
            runner.skipTry = false;
            runner.StartStage();
            yield return null;
            yield return null;
            Assert.AreEqual(StageRunner.State.Try, runner.Current);

            for (int i = 0; i < runner.stage.tryReps; i++)
                MoveEventHub.Publish(new MoveEvent(MoveId.Clap, 50.0 + i));
            Assert.AreEqual(StageRunner.State.CountIn, runner.Current, "4 claps → count-in");
        }
    }
}
