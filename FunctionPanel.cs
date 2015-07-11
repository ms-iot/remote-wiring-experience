using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace remote_wiring_experience
{
    class FunctionPanel : StackPanel
    {
        public FunctionPanel()
        {
            Orientation = Orientation.Vertical;
            FlowDirection = FlowDirection.LeftToRight;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Padding = new Thickness( 5 );
            Reset();
        }

        public void Reset()
        {
            Children.Clear();

            var button = new Button();
            var image = new Image();
            image.Source = new BitmapImage( new Uri( "ms-appx:///Assets/plus.png", UriKind.Absolute ) );
            image.Stretch = Stretch.None;
            button.Content = image;
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.Margin = new Thickness( 5 );
            button.Click += OnClick_AddFunctionButton;
            Children.Add( button );

            AddFunctionElement();
        }

        private void OnClick_AddFunctionButton( object sender, RoutedEventArgs e )
        {
            AddFunctionElement();
        }

        public void AddFunctionElement()
        {
            Children.Insert( Children.Count - 1, CreateFunctionElement() );
        }

        private static UIElement CreateFunctionElement()
        {
            var stack = new StackPanel();
            stack.Orientation = Orientation.Vertical;
            
            var text = new TextBlock();
            text.Text = "Then:";
            stack.Children.Add( text );

            var functionstack = new StackPanel();
            functionstack.Orientation = Orientation.Horizontal;
            functionstack.FlowDirection = FlowDirection.LeftToRight;
            functionstack.Margin = new Thickness( 5 );
            functionstack.BorderBrush = new SolidColorBrush( Color.FromArgb( 255, 0, 0, 0 ) );
            functionstack.BorderThickness = new Thickness( 2 );

            var itemstack = new StackPanel();
            itemstack.Margin = new Thickness( 5 );
            itemstack.Orientation = Orientation.Vertical;
            text = new TextBlock();
            text.Text = "Function:";
            itemstack.Children.Add( text );

            var combo = new ComboBox();
            foreach( Function function in Enum.GetValues( typeof( Function ) ) )
            {
                combo.Items.Add( new SingleFunction( function ) );
            }
            itemstack.Children.Add( combo );

            functionstack.Children.Add( itemstack );

            itemstack = new StackPanel();
            itemstack.Margin = new Thickness( 5 );

            text = new TextBlock();
            text.Text = "Target:";
            itemstack.Children.Add( text );

            combo = new ComboBox();
            combo.Items.Add( "Value" );
            combo.Items.Add( "Byte index" );
            itemstack.Children.Add( combo );

            functionstack.Children.Add( itemstack );

            var box = new TextBox();
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            box.VerticalAlignment = VerticalAlignment.Center;
            box.Margin = new Thickness( 5, 12, 5, 0 );
            functionstack.Children.Add( box );

            stack.Children.Add( functionstack );
            return stack;
        }
    }
}
