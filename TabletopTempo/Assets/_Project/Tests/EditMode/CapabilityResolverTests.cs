using System;
using NUnit.Framework;
using TabletopTempo.Core.Platform;

namespace TabletopTempo.Tests
{
    public class CapabilityResolverTests
    {
        private static readonly CapabilityProbeResult Quest3Like = new CapabilityProbeResult
        {
            HandTracking = true, Passthrough = true, ColorPassthrough = true,
            SceneModel = true, DepthApi = true, PassthroughCameraApi = true,
        };

        private static readonly CapabilityProbeResult Quest2Like = new CapabilityProbeResult
        {
            HandTracking = true, Passthrough = true, ColorPassthrough = false,
            SceneModel = true, DepthApi = false, PassthroughCameraApi = false,
        };

        [Test]
        public void UnknownProbe_FallsBackToQuest2Path()
        {
            var caps = CapabilityResolver.Resolve(CapabilityProbeResult.Unknown, OptionalFeatureFlags.AllOff, QualityOverride.Auto, "t");

            Assert.IsTrue(caps.HandTracking, "core: hands assumed");
            Assert.IsTrue(caps.Passthrough, "core: passthrough assumed");
            Assert.IsTrue(caps.SceneModel, "core: scene query allowed (falls back to touch calibration anyway)");
            Assert.IsFalse(caps.ColorPassthrough, "extras: colour not assumed");
            Assert.IsFalse(caps.DepthApiAvailable);
            Assert.IsFalse(caps.PassthroughCameraApiAvailable);
            Assert.IsFalse(caps.UseDepthRaycast);
            Assert.IsFalse(caps.UseCameraCupFinder);
            Assert.AreEqual(QualityTier.Low, caps.Tier);
        }

        [Test]
        public void Quest3Hardware_WithFlagsOff_UsesNoQuest3OnlyFeatures()
        {
            var caps = CapabilityResolver.Resolve(Quest3Like, OptionalFeatureFlags.AllOff, QualityOverride.Auto, "t");

            Assert.IsTrue(caps.DepthApiAvailable);
            Assert.IsTrue(caps.PassthroughCameraApiAvailable);
            Assert.IsFalse(caps.UseDepthRaycast, "flag off → depth not used");
            Assert.IsFalse(caps.UseCameraCupFinder, "flag off → camera cup finder not used");
            Assert.AreEqual(QualityTier.High, caps.Tier, "colour + depth hardware → High tier");
        }

        [Test]
        public void Quest3Hardware_WithFlagsOn_EnablesExtras()
        {
            var flags = new OptionalFeatureFlags { CameraCupFinder = true, DepthRaycast = true };
            var caps = CapabilityResolver.Resolve(Quest3Like, flags, QualityOverride.Auto, "t");

            Assert.IsTrue(caps.UseDepthRaycast);
            Assert.IsTrue(caps.UseCameraCupFinder);
        }

        [Test]
        public void FlagsOn_ButHardwareMissing_StaysOff()
        {
            var flags = new OptionalFeatureFlags { CameraCupFinder = true, DepthRaycast = true };
            var caps = CapabilityResolver.Resolve(Quest2Like, flags, QualityOverride.Auto, "t");

            Assert.IsFalse(caps.UseDepthRaycast);
            Assert.IsFalse(caps.UseCameraCupFinder);
            Assert.AreEqual(QualityTier.Low, caps.Tier);
        }

        [Test]
        public void ColourWithoutDepth_IsLowTier()
        {
            // e.g. a headset with colour passthrough but no depth sensor: stay on the safe budget.
            var probe = Quest2Like;
            probe.ColorPassthrough = true;
            var caps = CapabilityResolver.Resolve(probe, OptionalFeatureFlags.AllOff, QualityOverride.Auto, "t");
            Assert.AreEqual(QualityTier.Low, caps.Tier);
        }

        [Test]
        public void ExplicitlyMissingHandTracking_IsReported()
        {
            var probe = Quest2Like;
            probe.HandTracking = false; // e.g. hand tracking switched off in Quest settings
            var caps = CapabilityResolver.Resolve(probe, OptionalFeatureFlags.AllOff, QualityOverride.Auto, "t");
            Assert.IsFalse(caps.HandTracking);
        }

        [TestCase(QualityOverride.ForceLow, QualityTier.Low)]
        [TestCase(QualityOverride.ForceHigh, QualityTier.High)]
        public void QualityOverride_Wins(QualityOverride overrideValue, QualityTier expected)
        {
            var probe = overrideValue == QualityOverride.ForceLow ? Quest3Like : Quest2Like;
            var caps = CapabilityResolver.Resolve(probe, OptionalFeatureFlags.AllOff, overrideValue, "t");
            Assert.AreEqual(expected, caps.Tier);
        }

        [Test]
        public void SafeProbe_ThrowingProbe_ReturnsUnknown_AndLogs()
        {
            string logged = null;
            var result = CapabilityResolver.SafeProbe(new ThrowingProbe(), m => logged = m);

            Assert.IsFalse(result.HandTracking.HasValue);
            Assert.IsFalse(result.ColorPassthrough.HasValue);
            StringAssert.Contains("throwing", logged);
        }

        [Test]
        public void SafeProbe_NullProbe_ReturnsUnknown()
        {
            var result = CapabilityResolver.SafeProbe(null);
            Assert.IsFalse(result.Passthrough.HasValue);
        }

        [Test]
        public void Try_ConvertsExceptionToUnknown()
        {
            Assert.IsNull(CapabilityResolver.Try(() => throw new InvalidOperationException()));
            Assert.AreEqual(true, CapabilityResolver.Try(() => true));
            Assert.AreEqual(false, CapabilityResolver.Try(() => false));
        }

        [Test]
        public void Registry_HighestPriorityWins()
        {
            CapabilityProbeRegistry.Reset();
            try
            {
                var low = new FixedCapabilityProbe("low", Quest2Like);
                var high = new FixedCapabilityProbe("high", Quest3Like);
                CapabilityProbeRegistry.Register(high, 10);
                CapabilityProbeRegistry.Register(low, 0);
                Assert.AreSame(high, CapabilityProbeRegistry.Current);
            }
            finally
            {
                CapabilityProbeRegistry.Reset();
            }
        }

        private sealed class ThrowingProbe : ICapabilityProbe
        {
            public string Name => "throwing";
            public CapabilityProbeResult Probe() => throw new Exception("boom");
        }
    }
}
