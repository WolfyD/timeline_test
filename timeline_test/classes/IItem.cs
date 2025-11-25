using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeline_test.classes
{
    /// <summary>
    /// Interface for the items in the timeline
    /// </summary>
    public interface IItem
    {
        // Main section | generic data
        string Id { get; }
        ItemTypes ItemType { get; }
        string Title { get; }
        ItemDate StartDate { get; }
        ItemDate EndDate { get; }
        string Description { get; }
        string Content { get; }
        Tag[] Tags { get; }
        ItemImage[] Images { get; }
        string StoryId { get; }

        // Book reference section | citations
        string BookTitle { get; }
        string Chapter { get; }
        string Page { get; }
        Story[] StoryReferences { get; }

        // Display section | visuals
        Color Color { get; }
        bool ShowInNotes { get; }
        int Importance { get; }
    }



    public enum ItemTypes
    {
        Event,
        Period,
        Age,
        Picture,
        Note,
        Bookmark,
        Stop,
        Character
    }
}
