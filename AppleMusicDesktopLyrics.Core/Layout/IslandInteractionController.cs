using System;

namespace AppleMusicDesktopLyrics.Core.Layout
{
    public sealed class IslandInteractionController
    {
        private TimeSpan? enteredAt;
        private TimeSpan? leftAt;
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
                return IslandInteractionState.Collapsed;
            }

            return now - enteredAt.Value >= TimeSpan.FromMilliseconds(180)
                ? IslandInteractionState.Expanded
                : IslandInteractionState.Collapsed;
        }
    }
}
