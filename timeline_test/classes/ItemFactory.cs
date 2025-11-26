using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeline_test.classes
{
    public class ItemFactory : IItemFactory
    {
        private readonly Dictionary<string, Func<IItem>> _creators = new()
        {
            ["Event"] = () => new Event(),
            ["Period"] = () => new Period(),
            ["Age"] = () => new Age(),
            ["Picture"] = () => new Picture(),
            ["Note"] = () => new Note(),
            ["Bookmark"] = () => new Bookmark(),
            ["Character"] = () => new CharacterItem(),
            ["Timeline_start"] = () => new Stop(),
            ["Timeline_end"] = () => new Stop()
        };

        public IItem Create(string name)
        {
            if (!_creators.TryGetValue(name, out var create))
                throw new ArgumentException($"Unknown item type: {name}", nameof(name));

            return create();
        }
    }
}
