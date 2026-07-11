using System;

namespace AppleMusicDesktopLyrics.Core.Layout
{
    public sealed class IslandInteractionController
    {
        private TimeSpan? enteredAt;
        private TimeSpan? leftAt;
        private TimeSpan? expandedAt;
        private bool clickExpanded;
        private bool editing;

        public TimeSpan ExpandedDuration { get; set; } = TimeSpan.FromSeconds(5);

        public void PointerEntered(TimeSpan now)
        {
            if (!enteredAt.HasValue)
            {
                enteredAt = now;
            }
            leftAt = null;
        }

        public void PointerLeft(TimeSpan now)
        {
            leftAt = now;
        }

        public void SetEditing(bool value)
        {
            editing = value;
        }

        public void ToggleExpanded(TimeSpan now)
        {
            clickExpanded = !clickExpanded;
            enteredAt = now;
            expandedAt = clickExpanded ? now : (TimeSpan?)null;
            leftAt = null;
        }

        public IslandInteractionState GetState(TimeSpan now)
        {
            if (editing)
            {
                return IslandInteractionState.Editing;
            }

            if (enteredAt == null)
            {
                return IslandInteractionState.Collapsed;
            }

            if (clickExpanded && expandedAt.HasValue && now - expandedAt.Value >= ExpandedDuration)
            {
                clickExpanded = false;
                expandedAt = null;
                return IslandInteractionState.Collapsed;
            }

            return clickExpanded
                ? IslandInteractionState.Expanded
                : IslandInteractionState.Collapsed;
        }
    }
}
