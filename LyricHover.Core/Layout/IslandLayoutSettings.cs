namespace LyricHover.Core.Layout
{
    public sealed class IslandLayoutSettings
    {
        public IslandLayoutMode Mode { get; set; } = IslandLayoutMode.HorizontalBlocks;
        public IslandLayoutProfile Horizontal { get; set; }
        public IslandLayoutProfile CompactCollapsed { get; set; }
        public IslandLayoutProfile CompactExpanded { get; set; }

        public void Normalize()
        {
            Horizontal = Horizontal ?? IslandLayoutDefaults.CreateHorizontal();
            CompactCollapsed = CompactCollapsed ?? IslandLayoutDefaults.CreateCollapsed();
            CompactExpanded = CompactExpanded ?? IslandLayoutDefaults.CreateExpanded();
            Horizontal.Normalize();
            CompactCollapsed.Normalize();
            CompactExpanded.Normalize();
        }
    }
}
