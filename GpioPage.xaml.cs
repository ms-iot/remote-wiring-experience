using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.Maker.RemoteWiring;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace remote_wiring_experience
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GpioPage : Page
    {
        public GpioPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo( NavigationEventArgs e )
        {
            base.OnNavigatedTo( e );

            //we wan't to programatically create our UI so that we can support any Arduino type
            int numberOfRows = 3;
            int numberOfColumns = 2;
            int numberOfAnalogButtons = 6;

            for( int i = 0; i < numberOfRows; ++i )
            {
                var rowdefine = new RowDefinition();
                rowdefine.Height = new GridLength( 100, GridUnitType.Auto );
                ControlPanel.RowDefinitions.Add( rowdefine );
            }

            for( int i = 0; i < numberOfColumns; ++i )
            {
                var coldefine = new ColumnDefinition();
                coldefine.Width = new GridLength( 100, GridUnitType.Auto );
                ControlPanel.ColumnDefinitions.Add( coldefine );
            }

            for( int i = 0; i < numberOfAnalogButtons; ++i )
            {
                var stack = new StackPanel();
                stack.Orientation = Orientation.Horizontal;
                stack.FlowDirection = FlowDirection.LeftToRight;

                //set up the mode toggle button
                var togglebutton = new Button();
                var toggleimage = new Image();
                var bitmap = new BitmapImage();
                var uri = new Uri( BaseUri, @"Assets/none_a" + i + ".png" );
                bitmap.UriSource = uri;
                toggleimage.Source = bitmap;
                toggleimage.Stretch = Stretch.Uniform;
                togglebutton.Content = toggleimage;
                togglebutton.HorizontalAlignment = HorizontalAlignment.Left;
                togglebutton.VerticalAlignment = VerticalAlignment.Center;
                togglebutton.Padding = new Thickness();
                togglebutton.Margin = new Thickness( 5, 0, 5, 0 );
                stack.Children.Add( togglebutton );

                //set up the indication text
                var text = new TextBlock();
                text.HorizontalAlignment = HorizontalAlignment.Stretch;
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Text = "Tap to enable.";
                stack.Children.Add( text );

                ControlPanel.Children.Add( stack );
                Grid.SetRow( stack, i / 2 );
                Grid.SetColumn( stack, i % 2 );
            }
        }
    }
}
