using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AppleMusicDesktopLyrics.Core.Layout
{
    public sealed class LayoutEditSession
    {
        private readonly IslandLayoutProfile original;

        public LayoutEditSession(IslandLayoutProfile source)
        {
            original = Clone(source);
            Draft = Clone(source);
        }

        public IslandLayoutProfile Draft { get; private set; }

        public void Add(IslandModuleType type, int index)
        {
            if (type != IslandModuleType.Divider)
            {
                var existing = Draft.Modules.FirstOrDefault(module => module.Type == type);
                if (existing != null)
                {
                    Move(existing.Id, index);
                    return;
                }
            }

            Draft.Modules.Insert(Math.Max(0, Math.Min(index, Draft.Modules.Count)),
                new IslandModuleInstance(type));
        }

        public void Move(string id, int index)
        {
            var item = Draft.Modules.First(module => module.Id == id);
            Draft.Modules.Remove(item);
            Draft.Modules.Insert(Math.Max(0, Math.Min(index, Draft.Modules.Count)), item);
        }

        public void Remove(string id)
        {
            Draft.Modules.RemoveAll(module => module.Id == id);
        }

        public IslandLayoutProfile Commit()
        {
            return Clone(Draft);
        }

        public void Cancel()
        {
            Draft = Clone(original);
        }

        public static int FindInsertionIndex(
            double pointerX,
            IEnumerable<LayoutInsertionTarget> targets,
            double snapDistance)
        {
            var match = targets
                .Select(target => new { Target = target, Distance = Math.Abs(pointerX - target.X) })
                .Where(value => value.Distance <= snapDistance)
                .OrderBy(value => value.Distance)
                .FirstOrDefault();
            return match == null ? -1 : match.Target.Index;
        }

        private static IslandLayoutProfile Clone(IslandLayoutProfile profile)
        {
            var json = JsonSerializer.Serialize(profile ?? new IslandLayoutProfile());
            return JsonSerializer.Deserialize<IslandLayoutProfile>(json)
                ?? new IslandLayoutProfile();
        }
    }
}
