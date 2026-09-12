using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StateKeeper.Tests
{
    [TestClass]
    public sealed class ReleaseAuditTests
    {
        [TestMethod]
        public void DisabledCollectorRejectsHookEventsBeforeAccessingUnity()
        {
            // Bypass MonoBehaviour construction; disabled entry points must not touch Unity.
            var collector = (RunCollector)FormatterServices.GetUninitializedObject(typeof(RunCollector));
            var record = new RunRecord();
            typeof(RunCollector).GetField("_record", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(collector, record);
            collector.CollectionEnabled = false;
            collector.RecordSimpleEvent("PlayerDied", null, EventSource.RemoteObserved, "test");
            collector.RecordJump(null);
            collector.RecordItemEvent("ItemConsumed", null, null, null, EventSource.RemoteObserved, "test");
            collector.RecordItemOutcome("PlayerFriendHealed", null, null, null, null, 1, "test");
            collector.RecordItemSlotEvent("ItemPickedUp", null, null, EventSource.RemoteObserved, "test");
            collector.RecordCapturedItemEvent("ItemConsumed", new RunCollector.ItemEventCapture());
            Assert.IsNull(collector.CaptureItemEvent(null, null, null));
            Assert.AreEqual(0, record.events.Count);
            Assert.AreEqual(0, record.players.Count);
            Assert.AreEqual(0, record.samples.Count);
        }

        [TestMethod]
        public void DisablingExistingRecordingSchedulesAnExplicitResumeBoundary()
        {
            var collector = (RunCollector)FormatterServices.GetUninitializedObject(typeof(RunCollector));
            collector.CollectionEnabled = true;
            typeof(RunCollector).GetField("_record", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(collector, new RunRecord());
            collector.CollectionEnabled = false;
            collector.CollectionEnabled = true;
            Assert.AreEqual(true, typeof(RunCollector).GetField("_resumePending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(collector));
        }
    }
}
