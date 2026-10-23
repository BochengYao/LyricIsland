using System;
using System.Windows;
using LyricHover.Core.Layout;

namespace LyricHover.App.LayoutEditing
{
    public sealed class IslandLayoutDragPayload
    {
        public const string DataFormat = "LyricHover.IslandLayoutDragPayload.v1";

        public string OperationId { get; set; } = Guid.NewGuid().ToString("N");
        public IslandModuleType? NewType { get; set; }
        public string ExistingInstanceId { get; set; }

        public static DataObject CreateDataObject(IslandLayoutDragPayload payload)
        {
            var data = new DataObject();
            // Keep the payload application-private.  WPF's default auto-conversion can
            // expose the descriptor's ToString() value as text, which makes Windows add a
            // second generic document drag image (and a misleading "not allowed" badge)
            // beside our own module ghost.
            data.SetData(DataFormat, payload, false);
            OleDragImage.SuppressSystemImage(data);
            return data;
        }

        public static IslandLayoutDragPayload FromData(IDataObject data)
        {
            return data?.GetDataPresent(DataFormat, false) == true
                ? data.GetData(DataFormat, false) as IslandLayoutDragPayload
                : null;
        }
    }
}
