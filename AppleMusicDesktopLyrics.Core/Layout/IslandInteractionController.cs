using System;

namespace AppleMusicDesktopLyrics.Core.Layout
{
    public sealed class IslandInteractionController
    {
        private TimeSpan? enteredAt;
        private TimeSpan? leftAt;
        private bool clickExpanded;
        private bool editing;

        public void PointerEntered(TimeSpan now)
        {
            enteredAt = now;
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

            if (leftAt != null && now - leftAt.Value >= TimeSpan.FromMilliseconds(900))
            {
                clickExpanded = false;
                return IslandInteractionState.Collapsed;
            }

            return clickExpanded
                ? IslandInteractionState.Expanded
                : IslandInteractionState.Collapsed;
        }
    }
}
