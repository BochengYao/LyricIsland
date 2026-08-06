using System.Windows;
using System.Windows.Controls;

namespace LyricHover.App.LayoutEditing
{
    public partial class ModuleToolboxCard : UserControl
    {
        public static readonly DependencyProperty DescriptorProperty =
            DependencyProperty.Register(
                nameof(Descriptor),
                typeof(ModuleToolboxItemDescriptor),
                typeof(ModuleToolboxCard),
                new PropertyMetadata(null, DescriptorChanged));

        public ModuleToolboxCard()
        {
            InitializeComponent();
        }

        public ModuleToolboxItemDescriptor Descriptor
        {
            get => (ModuleToolboxItemDescriptor)GetValue(DescriptorProperty);
            set => SetValue(DescriptorProperty, value);
        }

        private static void DescriptorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var card = (ModuleToolboxCard)dependencyObject;
            var descriptor = e.NewValue as ModuleToolboxItemDescriptor;
            card.DataContext = descriptor;
            if (descriptor != null)
            {
                card.Width = descriptor.PreviewWidth;
            }
        }
    }
}
