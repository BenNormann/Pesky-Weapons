using System.Collections.Generic;
using NUnit.Framework;
using Pesky.Session.Rules;

namespace Pesky.Tests
{
    public sealed class RunLayoutPickerTests
    {
        [Test]
        public void GuaranteedRoomIsIncludedWithoutChangingRunLength()
        {
            List<ushort> pool = new List<ushort> { 1, 3, 4, 5, 6, 7, 8 };
            ushort[] layout = RunLayoutPicker.Pick(pool, new[] { 3 }, 5, 12345u);

            Assert.AreEqual(5, layout.Length);
            CollectionAssert.Contains(layout, (ushort)3);
            Assert.AreEqual(3, layout[0], "A configured room must keep its authored position at the front of the run.");
            Assert.AreEqual(layout.Length, new HashSet<ushort>(layout).Count);
        }

        [Test]
        public void FullyAuthoredRunKeepsTheSameContinuousOrderForEverySeed()
        {
            List<ushort> pool = new List<ushort> { 3, 4, 5, 6 };
            int[] authoredOrder = { 3, 4, 5, 6 };

            ushort[] first = RunLayoutPicker.Pick(pool, authoredOrder, 4, 1u);
            ushort[] second = RunLayoutPicker.Pick(pool, authoredOrder, 4, 987654321u);

            CollectionAssert.AreEqual(new ushort[] { 3, 4, 5, 6 }, first);
            CollectionAssert.AreEqual(first, second, "Starting another round must not reshuffle the authored four-room run.");
        }

        [Test]
        public void MissingAndDuplicateGuaranteesAreIgnored()
        {
            List<ushort> pool = new List<ushort> { 10, 11, 12 };
            ushort[] layout = RunLayoutPicker.Pick(pool, new[] { 99, 11, 11, -1 }, 3, 9u);

            CollectionAssert.AreEquivalent(new ushort[] { 10, 11, 12 }, layout);
        }

        [Test]
        public void SameSeedProducesSameLayout()
        {
            List<ushort> pool = new List<ushort> { 1, 2, 3, 4, 5, 6, 7, 8 };
            ushort[] a = RunLayoutPicker.Pick(pool, new[] { 3 }, 5, 77u);
            ushort[] b = RunLayoutPicker.Pick(pool, new[] { 3 }, 5, 77u);

            CollectionAssert.AreEqual(a, b);
        }
    }
}
