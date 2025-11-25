using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeline_test.classes
{
    public class Note : IItem
    {
        public string Id { get; set; }

        public ItemTypes ItemType => ItemTypes.Note;

        public string Title { get; set; }

        public ItemDate StartDate { get; set; }

        public ItemDate EndDate { get; set; }

        public string Description { get; set; }

        public string Content { get; set; }

        public Tag[] Tags { get; set; }

        public ItemImage[] Images { get; set; }

        public string StoryId { get; set; }

        public string BookTitle { get; set; }

        public string Chapter { get; set; }

        public string Page { get; set; }

        public Story[] StoryReferences { get; set; }

        public Color Color { get; set; }

        public bool ShowInNotes { get; set; }

        public int Importance { get; set; }
    }
}
