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
            data.SetData(DataFormat, payload);
            return data;
        }

        public static IslandLayoutDragPayload FromData(IDataObject data)
        {
            return data?.GetDataPresent(DataFormat) == true
                ? data.GetData(DataFormat) as IslandLayoutDragPayload
                : null;
        }
    }
}
