using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace LyricHover.Core.Layout
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
            Draft.Modules.Insert(Math.Max(0, Math.Min(index, Draft.Modules.Count)),
                new IslandModuleInstance(type));
        }

        public void Move(string id, int index)
        {
            var sourceIndex = Draft.Modules.FindIndex(module => module.Id == id);
            if (sourceIndex < 0)
            {
                return;
            }

            var insertionIndex = Math.Max(0, Math.Min(index, Draft.Modules.Count));
            var item = Draft.Modules[sourceIndex];
            Draft.Modules.RemoveAt(sourceIndex);
            if (sourceIndex < insertionIndex)
            {
                insertionIndex--;
            }

            Draft.Modules.Insert(Math.Max(0, Math.Min(insertionIndex, Draft.Modules.Count)), item);
        }

        public void Remove(string id)
        {
            Draft.Modules.RemoveAll(module => module.Id == id);
        }

        public IslandLayoutProfile Commit()
        {
            return Clone(Draft);
        }

        public IslandLayoutProfile GetDraftSnapshot()
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
