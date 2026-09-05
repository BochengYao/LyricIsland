using System;

namespace LyricsIsland.App.LayoutEditing
{
    public sealed class ModuleDragCompletedEventArgs : EventArgs
    {
        public ModuleDragCompletedEventArgs(
            string instanceId,
            string operationId,
            bool droppedOutside,
            bool cancelled)
        {
            InstanceId = instanceId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            DroppedOutside = droppedOutside;
            Cancelled = cancelled;
        }

        public string InstanceId { get; }

        public string OperationId { get; }

        public bool DroppedOutside { get; }

        public bool Cancelled { get; }
    }
}
