using NUnit.Framework;
using timeline_test.classes;

namespace StoryTimeline.Tests
{
    [TestFixture]
    public class ItemFactoryTests
    {
        /// <summary>
        /// Success
        /// </summary>
        [Test]
        public void Create_Event_Returns_EventItem()
        {
            var factory = new ItemFactory();
            var item = factory.Create("Event");
            Assert.That(item, Is.InstanceOf(typeof(Event)));
        }

        [Test]
        public void Create_Period_Returns_PeriodItem()
        {
            var factory = new ItemFactory();
            var item = factory.Create("Period");
            Assert.That(item, Is.InstanceOf(typeof(Period)));
        }

        [Test]
        public void Create_Age_Returns_AgeItem()
        {
            var factory = new ItemFactory();
            var item = factory.Create("Age");
            Assert.That(item, Is.InstanceOf(typeof(Age)));
        }

        [Test]
        public void Create_Picture_Returns_PictureItem()
        {
            var factory = new ItemFactory();
            var item = factory.Create("Picture");
            Assert.That(item, Is.InstanceOf(typeof(Picture)));
        }

        [Test]
        public void Create_Note_Returns_NoteItem()
        {
            var factory = new ItemFactory();
            var item = factory.Create("Note");
            Assert.That(item, Is.InstanceOf(typeof(Note)));
        }

        [Test]
        public void Create_Bookmark_Returns_BookmarkItem()
        {
            var factory = new ItemFactory();
            var item = factory.Create("Bookmark");
            Assert.That(item, Is.InstanceOf(typeof(Bookmark)));
        }

        [Test]
        public void Create_Character_Returns_CharacterItem()
        {
            var factory = new ItemFactory();
            var item = factory.Create("Character");
            Assert.That(item, Is.InstanceOf(typeof(CharacterItem)));
        }

        [Test]
        public void Create_TimelineStart_Returns_StopItem()
        {
            var factory = new ItemFactory();
            var item = factory.Create("Timeline_start");
            Assert.That(item, Is.InstanceOf(typeof(Stop)));
        }

        [Test]
        public void Create_TimelineEnd_Returns_StopItem()
        {
            var factory = new ItemFactory();
            var item = factory.Create("Timeline_end");
            Assert.That(item, Is.InstanceOf(typeof(Stop)));
        }

        /// <summary>
        /// Fail
        /// </summary>
        [Test]
        public void Create_UnknownType_Throws()
        {
            var factory = new ItemFactory();

            Assert.Throws<System.ArgumentException>(() =>
            {
                factory.Create("????");
            });
        }
    }
}
