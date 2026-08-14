using System;
using GritGud.Presentation.Levels.Persistence;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class PlayerPrefsLevelDraftStoreTests
    {
        [Test]
        public void DraftRoundTripUsesAnIsolatedSlot()
        {
            var store = new PlayerPrefsLevelDraftStore();
            string slot = "test-" + Guid.NewGuid().ToString("N");

            try
            {
                store.SaveDraft(slot, "{\"schemaVersion\":1}");

                Assert.That(store.HasDraft(slot), Is.True);
                Assert.That(store.LoadDraft(slot), Is.EqualTo("{\"schemaVersion\":1}"));
            }
            finally
            {
                store.DeleteDraft(slot);
            }
        }

        [Test]
        public void OversizedDraftIsRejectedBeforeWriting()
        {
            var store = new PlayerPrefsLevelDraftStore();
            string slot = "test-" + Guid.NewGuid().ToString("N");
            string oversized = new string('x', PlayerPrefsLevelDraftStore.MaximumDraftCharacters + 1);

            try
            {
                Assert.Throws<InvalidOperationException>(() => store.SaveDraft(slot, oversized));
                Assert.That(store.HasDraft(slot), Is.False);
            }
            finally
            {
                store.DeleteDraft(slot);
            }
        }
    }
}
