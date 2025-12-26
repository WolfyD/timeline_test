using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeline.classes
{
    /// <summary>
    /// Interface for the items in the timeline
    /// </summary>
    public interface IItem
    {
        // Main section | generic data
        string Id { get; set; }
        ItemTypes ItemType { get; set; }
        string Title { get; set; }
        ItemDate StartDate { get; set; }
        ItemDate EndDate { get; set; }
        string Description { get; set; }
        string Content { get; set; }
        Tag[] Tags { get; set; }
        ItemImage[] Images { get; set; }
        string StoryId { get; set; }

        // Book reference section | citations
        string BookTitle { get; set; }
        string Chapter { get; set; }
        string Page { get; set; }
        Story[] StoryReferences { get; set; }

        // Display section | visuals
        Color Color { get; set; }
        bool ShowInNotes { get; set; }
        int Importance { get; set; }
        int ItemIndex { get; set; }

        // In case timeline granularity changed
        int CreationGranularity { get; set; }
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
