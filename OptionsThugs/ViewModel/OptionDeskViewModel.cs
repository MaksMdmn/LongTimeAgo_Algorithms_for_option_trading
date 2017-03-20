using System.Windows;
using StockSharp.Xaml;

namespace OptionsThugs.ViewModel
{
    public class OptionDeskViewModel : DependencyObject
    {
        public OptionDeskModel DeskModel
        {
            get { return (OptionDeskModel)GetValue(DeskModelProperty); }
            set { SetValue(DeskModelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DeskModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DeskModelProperty =
            DependencyProperty.Register("DeskModel", typeof(OptionDeskModel), typeof(OptionDeskViewModel), new PropertyMetadata(null));




    }
}