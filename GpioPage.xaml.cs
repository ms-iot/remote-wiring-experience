using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Collections.Generic;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Maker.RemoteWiring;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace remote_wiring_experience
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GpioPage : Page
    {
        /*
         * we wan't to programatically create our UI so that we can eventually support any Arduino type
         * but for now we will define our values as constant member variables
         */
        private const int numberOfAnalogButtons = 6;
        private const int numberOfDigitalButtons = 14;
        private const int numberOfAnalogSliders = 7;
        private byte[] analogSliders = { 3, 5, 6, 9, 10, 11, 13 };

        //stores image assets so that they can be loaded once and reused many times
        private Dictionary<String, BitmapImage> bitmaps;

        //remembers what UI control elements have been loaded
        private Dictionary<String, bool> uiControlsLoaded;

        public GpioPage()
        {
            this.InitializeComponent();
            bitmaps = new Dictionary<string, BitmapImage>();
            uiControlsLoaded = new Dictionary<string, bool>();
            foreach( var item in DeviceControlPivot.Items )
            {
                uiControlsLoaded.Add( ( (PivotItem)item ).Name, false );
            }
        }

        protected override void OnNavigatedTo( NavigationEventArgs e )
        {
            base.OnNavigatedTo( e );
            loadAssets();
        }



        //******************************************************************************
        //* UI Support Functions
        //******************************************************************************

        /// <summary>
        /// This function loads all of the necessary bitmaps that will be used by this program into the resource dictionary
        /// </summary>
        private void loadAssets()
        {
            bitmaps.Add( "high", new BitmapImage( new Uri( BaseUri, @"Assets/high.png" ) ) );
            bitmaps.Add( "low", new BitmapImage( new Uri( BaseUri, @"Assets/low.png" ) ) );
            bitmaps.Add( "analog", new BitmapImage( new Uri( BaseUri, @"Assets/analog.png" ) ) );

            for( int i = 0; i < numberOfAnalogButtons; ++i )
            {
                bitmaps.Add( "none_a" + i, new BitmapImage( new Uri( BaseUri, @"Assets/none_a" + i + ".png" ) ) );
                bitmaps.Add( "disabled_a" + i, new BitmapImage( new Uri( BaseUri, @"Assets/disabled_a" + i + ".png" ) ) );
                bitmaps.Add( "input_a" + i, new BitmapImage( new Uri( BaseUri, @"Assets/input_a" + i + ".png" ) ) );
            }

            for( int i = 0; i < numberOfDigitalButtons; ++i )
            {
                bitmaps.Add( "output_" + i, new BitmapImage( new Uri( BaseUri, @"Assets/output_" + i + ".png" ) ) );
                bitmaps.Add( "disabled_" + i, new BitmapImage( new Uri( BaseUri, @"Assets/disabled_" + i + ".png" ) ) );
                bitmaps.Add( "input_" + i, new BitmapImage( new Uri( BaseUri, @"Assets/input_" + i + ".png" ) ) );
            }
        }


        /// <summary>
        /// This function is called when a page is loaded either by swipe navigation or clicking the tabs at the top
        /// </summary>
        /// <param name="sender">The pivot which is loading the item</param>
        /// <param name="args">relative arguments, including the item that is being loaded</param>
        private void Pivot_PivotItemLoaded( Pivot sender, PivotItemEventArgs args )
        {
            if( uiControlsLoaded[args.Item.Name] ) return;

            switch( args.Item.Name )
            {
                case "Digital":
                    loadDigitalControls();
                    break;

                case "Analog":
                    loadAnalogControls();
                    break;

                case "I2C":
                    loadI2cControls();
                    break;
            }
            uiControlsLoaded[args.Item.Name] = true;
        }

        /// <summary>
        /// Adds the necessary analog controls to the analog pivot page, this will only be called the first time this pivot page is loaded
        /// </summary>
        private void loadAnalogControls()
        {
            for( int i = 0; i < numberOfAnalogButtons; ++i )
            {
                var stack = new StackPanel();
                stack.Orientation = Orientation.Horizontal;
                stack.FlowDirection = FlowDirection.LeftToRight;

                //set up the mode toggle button
                var button = new Button();
                var image = new Image();
                image.Source = bitmaps[ "none_a" + i ];
                image.Stretch = Stretch.Uniform;
                button.Content = image;
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.VerticalAlignment = VerticalAlignment.Center;
                button.Padding = new Thickness();
                button.Margin = new Thickness( 5, 0, 5, 0 );
                stack.Children.Add( button );

                //set up the indication text
                var text = new TextBlock();
                text.HorizontalAlignment = HorizontalAlignment.Stretch;
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Text = "Tap to enable.";
                stack.Children.Add( text );

                AnalogControls.Children.Add( stack );
            }
        }

        /// <summary>
        /// Adds the necessary digital controls to the digital pivot page, this will only be called the first time this pivot page is loaded
        /// </summary>
        private void loadDigitalControls()
        {
            for( int i = 0; i < numberOfDigitalButtons; ++i )
            {
                var stack = new StackPanel();
                stack.Orientation = Orientation.Horizontal;
                stack.FlowDirection = FlowDirection.LeftToRight;

                //set up the mode toggle button
                var name = "output_" + i;
                var button = new Button();
                var image = new Image();
                image.Source = bitmaps[ name ];
                image.Stretch = Stretch.Uniform;
                button.Content = image;
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.VerticalAlignment = VerticalAlignment.Center;
                button.Padding = new Thickness(); ;
                button.Margin = new Thickness( 5, 0, 5, 0 ); ;
                button.Name = name;
                stack.Children.Add( button );

                //set up the state toggle button
                name = "digitalstate_" + i;
                button = new Button();
                image = new Image();
                image.Source = bitmaps["low"];
                image.Stretch = Stretch.Uniform;
                button.Content = image;
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.VerticalAlignment = VerticalAlignment.Center;
                button.Padding = new Thickness();
                button.Margin = new Thickness( 5, 0, 5, 0 );
                button.Name = name;
                stack.Children.Add( button );
                
                DigitalControls.Children.Add( stack );
            }
        }

        /// <summary>
        /// Adds the necessary i2c controls to the i2c pivot page, this will only be called the first time this pivot page is loaded
        /// </summary>
        private void loadI2cControls()
        {
            //throw new NotImplementedException();
        }
    }
}
