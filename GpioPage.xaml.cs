using System;
using System.Text;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Controls.Primitives;
using Microsoft.Maker.RemoteWiring;
using System.Diagnostics;

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
        private const int numberOfAnalogPins = 6;
        private const int numberOfDigitalPins = 14;
        private static byte[] pwmPins = { 3, 5, 6, 9, 10, 11, 13 };
        private int numberOfPwmPins = pwmPins.Length;
        private static byte[] i2cPins = { 18, 19 };
        private bool isI2cEnabled = false;

        private FunctionPanel functionPanel;

        //stores image assets so that they can be loaded once and reused many times
        private Dictionary<String, BitmapImage> bitmaps;

        //remembers what UI control elements have been loaded
        private Dictionary<String, bool> uiControlsLoaded;

        //these dictionaries store the loaded UI elements for easy access by pin number
        private Dictionary<byte, Image> digitalModeImages;
        private Dictionary<byte, Image> digitalStateImages;
        private Dictionary<byte, Image> analogModeImages;
        private Dictionary<byte, TextBlock> analogTextBlocks;
        private Dictionary<byte, Image> pwmModeImages;
        private Dictionary<byte, Slider> pwmSliders;


        private RemoteDevice arduino;

        public GpioPage()
        {
            this.InitializeComponent();

            bitmaps = new Dictionary<string, BitmapImage>();
            uiControlsLoaded = new Dictionary<string, bool>();
            pwmSliders = new Dictionary<byte, Slider>();
            digitalModeImages = new Dictionary<byte, Image>();
            digitalStateImages = new Dictionary<byte, Image>();
            analogModeImages = new Dictionary<byte, Image>();
            analogTextBlocks = new Dictionary<byte, TextBlock>();
            pwmModeImages = new Dictionary<byte, Image>();

            foreach( var item in DeviceControlPivot.Items )
            {
                uiControlsLoaded.Add( ( (PivotItem)item ).Name, false );
            }
        }

        protected override void OnNavigatedTo( NavigationEventArgs e )
        {
            base.OnNavigatedTo( e );
            loadAssets();
            arduino = App.Arduino;
            arduino.DigitalPinUpdatedEvent += Arduino_DigitalPinUpdatedEvent;
            arduino.AnalogPinUpdatedEvent += Arduino_AnalogPinUpdatedEvent;
        }


        //******************************************************************************
        //* Windows Remote Arduino callbacks
        //******************************************************************************

        /// <summary>
        /// This function is called when the Windows Remote Arduino library reports that an input value has changed for an analog pin.
        /// </summary>
        /// <param name="pin">The pin whose value has changed</param>
        /// <param name="value">the new value of the pin</param>
        private void Arduino_AnalogPinUpdatedEvent( byte pin, ushort value )
        {
            //we must dispatch the change to the UI thread to update the text field.
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                UpdateAnalogTextBlockValue( pin, value );
            } ) );
        }


        /// <summary>
        /// This function is called when the Windows Remote Arduino library reports that an input value has changed for a digital pin.
        /// </summary>
        /// <param name="pin">The pin whose value has changed</param>
        /// <param name="state">the new state of the pin, either HIGH or LOW</param>
        private void Arduino_DigitalPinUpdatedEvent( byte pin, PinState state )
        {
            //we must dispatch the change to the UI thread to change the indicator image
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                UpdateDigitalPinStateIndicator( pin );
            } ) );
        }

        /// <summary>
        /// This function is called when the Windows Remote Arduino library receives a reply from the device in response to an I2C read request.
        /// </summary>
        /// <param name="address_">The address which is replying</param>
        /// <param name="reg_">The register which is replying</param>
        /// <param name="response">A datareader containing the raw reponse bytes from the device.</param>
        private void I2c_I2cReplyEvent( byte address_, byte reg_, Windows.Storage.Streams.DataReader response )
        {
            //we must dispatch the change to the UI thread to change the indicator image
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                var builder = new StringBuilder();
                var bytes = new byte[response.UnconsumedBufferLength];
                response.ReadBytes( bytes );
                
                for( int i = 0; i < bytes.Length; ++i )
                {
                    builder.Append( "[" ).Append( bytes[i].ToString( "x" ) ).Append( "] " );
                }
                I2cRawReplyTextBox.Text = builder.ToString().TrimEnd();

                try
                {
                    double processed = functionPanel.ProcessI2cReply( bytes );
                    I2cProcessedReplyTextBox.Text = processed.ToString();
                }
                catch( Exception )
                {
                    I2cProcessedReplyTextBox.Text = "";
                }
            } ) );
        }


        //******************************************************************************
        //* Button Callbacks
        //******************************************************************************


        /// <summary>
        /// Invoked when the analog mode toggle button is tapped or pressed
        /// </summary>
        /// <param name="sender">the button being pressed</param>
        /// <param name="args">button press event args</param>
        private void OnClick_AnalogModeToggleButton( object sender, RoutedEventArgs args )
        {
            var button = sender as Button;
            var image = button.Content as Image;
            var pin = GetPinFromButtonObject( button );
            var analogPinNumber = ConvertAnalogPinToPinNumber( pin );

            var mode = arduino.getPinMode( analogPinNumber );
            var nextMode = ( mode == PinMode.OUTPUT ) ? PinMode.ANALOG : PinMode.OUTPUT;

            arduino.pinMode( analogPinNumber, nextMode );

            //two of the analog pins are also the I2C pins (SDA and SCL), so if this pin is one of those, we just disabled I2C
            if( isI2cEnabled )
            {
                if( i2cPins[0] == analogPinNumber )
                {
                    isI2cEnabled = false;
                    arduino.pinMode( i2cPins[1], PinMode.OUTPUT );
                    arduino.I2c.I2cReplyEvent -= I2c_I2cReplyEvent;
                }
                else if( i2cPins[1] == analogPinNumber )
                {
                    isI2cEnabled = false;
                    arduino.pinMode( i2cPins[0], PinMode.OUTPUT );
                    arduino.I2c.I2cReplyEvent -= I2c_I2cReplyEvent;
                }
            }

            UpdateAnalogPinModeIndicator( pin );
        }


        /// <summary>
        /// Invoked when the analog mode toggle button is tapped or pressed
        /// </summary>
        /// <param name="sender">the button being pressed</param>
        /// <param name="args">button press event args</param>
        private void OnClick_DigitalModeToggleButton( object sender, RoutedEventArgs args )
        {
            var button = sender as Button;
            var image = button.Content as Image;
            var pin = GetPinFromButtonObject( button );

            var mode = arduino.getPinMode( pin );
            var nextMode = ( mode == PinMode.OUTPUT ) ? PinMode.INPUT : PinMode.OUTPUT;

            arduino.pinMode( pin, nextMode );
            UpdateDigitalPinModeIndicator( pin );
        }

        /// <summary>
        /// Invoked when the digital state toggle button is tapped or pressed
        /// </summary>
        /// <param name="sender">the button being pressed</param>
        /// <param name="args">button press event args</param>
        private void OnClick_DigitalStateToggleButton( object sender, RoutedEventArgs args )
        {
            var button = sender as Button;
            var image = button.Content as Image;
            var pin = GetPinFromButtonObject( button );

            if( arduino.getPinMode( pin ) != PinMode.OUTPUT )
            {
                ShowToast( "Incorrect PinMode!", "You must first set this pin to OUTPUT", null );
                return;
            }

            var state = arduino.digitalRead( pin );
            var nextState = ( state == PinState.HIGH ) ? PinState.LOW : PinState.HIGH;

            arduino.digitalWrite( pin, nextState );
            UpdateDigitalPinStateIndicator( pin );
        }

        /// <summary>
        /// Invoked when the slider value for a PWM pin is modified.
        /// </summary>
        /// <param name="sender">the slider being manipulated</param>
        /// <param name="args">slider value changed event args</param>
        private void OnValueChanged_PwmSlider( object sender, RangeBaseValueChangedEventArgs args )
        {
            var slider = sender as Slider;
            var pin = Convert.ToByte( slider.Name.Substring( slider.Name.IndexOf( '_' ) + 1 ) );

            arduino.analogWrite( pin, (byte)args.NewValue );
        }

        private void OnClick_PwmModeToggleButton( object sender, RoutedEventArgs args )
        {
            var button = sender as Button;
            var pin = GetPinFromButtonObject( button );

            var mode = arduino.getPinMode( pin );
            var nextMode = ( mode == PinMode.PWM ) ? PinMode.INPUT : PinMode.PWM;

            arduino.pinMode( pin, nextMode );
            UpdatePwmPinModeIndicator( pin );
            pwmSliders[pin].Visibility = ( nextMode == PinMode.PWM ) ? Visibility.Visible : Visibility.Collapsed;
        }


        private void OnClick_I2cToggleButton( object sender, RoutedEventArgs e )
        {
            if( !isI2cEnabled )
            {
                arduino.I2c.enable();
                arduino.I2c.I2cReplyEvent += I2c_I2cReplyEvent;
                isI2cEnabled = true;
            }
            UpdateI2cControls();
        }

        private void OnClick_I2cWriteButton( object sender, RoutedEventArgs e )
        {
            try
            {
                uint address;
                uint register;
                if( !VerifyAddressAndRegister( out address, out register ) ) return;

                //it is OK to not send any data with your message
                string message = I2cWriteDataTextBox.Text;
                uint[] bytes = null;

                if( !string.IsNullOrEmpty( message ) )
                {
                    string[] byteStrings = I2cWriteDataTextBox.Text.Split( new char[] { ' ' } );
                    bytes = new uint[byteStrings.Length];
                    for( int i = 0; i < byteStrings.Length; ++i )
                    {
                        bytes[i] = ParsePositiveDecimalValueOrThrow( byteStrings[i] );
                        if( bytes[i] > 255 )
                        {
                            ShowToast( "Value too large.", "Byte values cannot be larger than 255", null );
                            return;
                        }
                    }
                }

                arduino.I2c.beginTransmission( (byte)address );
                arduino.I2c.write( (byte)register );
                if( bytes != null )
                {
                    foreach( int val in bytes )
                    {
                        arduino.I2c.write( (byte)val );
                    }
                }
                arduino.I2c.endTransmission();
            }
            catch( FormatException )
            {
                ShowToast( "Invalid address, register, or data", "Enter numbers in decimal, hex, or binary", null );
            }
        }

        private void OnClick_I2cReadButton( object sender, RoutedEventArgs e )
        {
            if( string.IsNullOrEmpty( I2cReadQuantityTextBox.Text ) )
            {
                ShowToast( "Nothing sent.", "You must specify how many bytes to read.", null );
                return;
            }

            try
            {
                uint address;
                uint register;
                if( !VerifyAddressAndRegister( out address, out register ) ) return;

                uint read = ParsePositiveDecimalValueOrThrow( I2cReadQuantityTextBox.Text );
                if( read > 255 )
                {
                    ShowToast( "Value too large.", "Byte values cannot be larger than 255", null );
                    return;
                }

                arduino.I2c.requestFrom( (byte)address, (byte)read );

            }
            catch( FormatException )
            {
                ShowToast( "Invalid read quantity", "You must enter a number value.", null );
            }
        }

        /// <summary>
        /// This function is invoked when the Read Quantity text box is modified
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTextChanged_I2cReadQuantityTextBox( object sender, TextChangedEventArgs e )
        {
            I2cReadButton.IsEnabled = !string.IsNullOrEmpty( I2cReadQuantityTextBox.Text );

            uint num = 0;
            try
            {
                num = ParsePositiveDecimalValueOrThrow( I2cReadQuantityTextBox.Text );
            }
            catch( FormatException )
            {
                I2cReadButton.IsEnabled = false;
            }

            functionPanel.NumberOfBytes = num;
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
            bitmaps.Add( "enabled", new BitmapImage( new Uri( BaseUri, @"Assets/enabled.png" ) ) );
            bitmaps.Add( "enablei2c", new BitmapImage( new Uri( BaseUri, @"Assets/enablei2c.png" ) ) );

            for( int i = 0; i < numberOfAnalogPins; ++i )
            {
                bitmaps.Add( "none_a" + i, new BitmapImage( new Uri( BaseUri, @"Assets/none_a" + i + ".png" ) ) );
                bitmaps.Add( "disabled_a" + i, new BitmapImage( new Uri( BaseUri, @"Assets/disabled_a" + i + ".png" ) ) );
                bitmaps.Add( "input_a" + i, new BitmapImage( new Uri( BaseUri, @"Assets/input_a" + i + ".png" ) ) );
            }

            for( int i = 0; i < numberOfDigitalPins; ++i )
            {
                bitmaps.Add( "output_" + i, new BitmapImage( new Uri( BaseUri, @"Assets/output_" + i + ".png" ) ) );
                bitmaps.Add( "disabled_" + i, new BitmapImage( new Uri( BaseUri, @"Assets/disabled_" + i + ".png" ) ) );
                bitmaps.Add( "input_" + i, new BitmapImage( new Uri( BaseUri, @"Assets/input_" + i + ".png" ) ) );
            }

            for( int i = 0; i < numberOfPwmPins; ++i )
            {
                bitmaps.Add( "pwm_" + pwmPins[i], new BitmapImage( new Uri( BaseUri, @"Assets/pwm_" + pwmPins[i] + ".png" ) ) );
            }
        }


        /// <summary>
        /// This function is called when a page is loaded either by swipe navigation or clicking the tabs at the top
        /// </summary>
        /// <param name="sender">The pivot which is loading the item</param>
        /// <param name="args">relative arguments, including the item that is being loaded</param>
        private void Pivot_PivotItemLoaded( Pivot sender, PivotItemEventArgs args )
        {
            switch( args.Item.Name )
            {
                case "Digital":
                    UpdateDigitalControls();
                    break;

                case "Analog":
                    UpdateAnalogControls();
                    break;

                case "I2C":
                    UpdateI2cControls();
                    break;
            }
            uiControlsLoaded[args.Item.Name] = true;
        }

        /// <summary>
        /// Updates the UI for the analog control page as necessary
        /// </summary>
        private void UpdateAnalogControls()
        {
            if( !uiControlsLoaded["Analog"] ) loadAnalogControls();
            for( byte pin = 0; pin < numberOfAnalogPins; ++pin )
            {
                UpdateAnalogPinModeIndicator( pin );
            }

            for( byte i = 0; i < numberOfPwmPins; ++i )
            {
                UpdatePwmPinModeIndicator( pwmPins[i] );
            }
        }

        /// <summary>
        /// Updates the UI for the digital control page as necessary
        /// </summary>
        private void UpdateDigitalControls()
        {
            if( !uiControlsLoaded["Digital"] ) loadDigitalControls();
            for( byte pin = 0; pin < numberOfDigitalPins; ++pin )
            {
                UpdateDigitalPinModeIndicator( pin );
                UpdateDigitalPinStateIndicator( pin );
            }
        }

        /// <summary>
        /// Updates the UI for the I2C control page as necessary
        /// </summary>
        private void UpdateI2cControls()
        {
            if( !uiControlsLoaded["I2C"] ) loadI2cControls();

            if( isI2cEnabled )
            {
                I2cToggleImage.Source = bitmaps["enabled"];

                I2cVisibilityPanel.Visibility = Visibility.Visible;
                I2cReplyProcessPanel.Children.Clear();
                functionPanel = new FunctionPanel( 0 );
                I2cReplyProcessPanel.Children.Add( functionPanel );
            }
            else
            {
                I2cToggleImage.Source = bitmaps["enablei2c"];
                I2cVisibilityPanel.Visibility = Visibility.Collapsed;
            }
        }


        /// <summary>
        /// Adds the necessary analog controls to the analog pivot page, this will only be called the first time this pivot page is loaded
        /// </summary>
        private void loadAnalogControls()
        {
            //add controls and text fields for each analog pin the board supports
            for( byte i = 0; i < numberOfAnalogPins; ++i )
            {
                var stack = new StackPanel();
                stack.Orientation = Orientation.Horizontal;
                stack.FlowDirection = FlowDirection.LeftToRight;

                //set up the mode toggle button
                var button = new Button();
                var image = new Image();
                image.Stretch = Stretch.Uniform;
                analogModeImages.Add( i, image );
                button.Content = image;
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.VerticalAlignment = VerticalAlignment.Center;
                button.Padding = new Thickness();
                button.Margin = new Thickness( 5, 0, 5, 0 );
                button.Name = "analogmode_" + i;
                button.Click += OnClick_AnalogModeToggleButton;
                stack.Children.Add( button );

                //set up the indication text
                var text = new TextBlock();
                text.HorizontalAlignment = HorizontalAlignment.Stretch;
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Text = "Tap to enable.";
                analogTextBlocks.Add( i, text );
                stack.Children.Add( text );

                AnalogControls.Children.Add( stack );
            }

            //add controls and value sliders for each pwm pin the board supports
            for( byte i = 0; i < numberOfPwmPins; ++i )
            {
                var stack = new StackPanel();
                stack.Orientation = Orientation.Horizontal;
                stack.FlowDirection = FlowDirection.LeftToRight;
                stack.HorizontalAlignment = HorizontalAlignment.Stretch;

                //set up the mode toggle button
                var button = new Button();
                var image = new Image();
                image.Stretch = Stretch.Uniform;
                pwmModeImages.Add( pwmPins[i], image );
                button.Content = image;
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.VerticalAlignment = VerticalAlignment.Center;
                button.Padding = new Thickness();
                button.Margin = new Thickness( 5, 0, 5, 0 );
                button.Name = "pwm_" + pwmPins[i];
                button.Click += OnClick_PwmModeToggleButton;
                stack.Children.Add( button );

                //set up the value change slider
                var slider = new Slider();
                slider.Visibility = Visibility.Collapsed;
                slider.Orientation = Orientation.Horizontal;
                slider.HorizontalAlignment = HorizontalAlignment.Stretch;
                slider.SmallChange = 32;
                slider.StepFrequency = 32;
                slider.TickFrequency = 32;
                slider.ValueChanged += OnValueChanged_PwmSlider;
                slider.Minimum = 0;
                slider.Maximum = 255;
                slider.Name = "slider_" + pwmPins[i];
                slider.Width = 220;
                pwmSliders.Add( pwmPins[i], slider );
                stack.Children.Add( slider );

                AnalogControls.Children.Add( stack );
            }
        }


        /// <summary>
        /// Adds the necessary digital controls to the digital pivot page, this will only be called the first time this pivot page is loaded
        /// </summary>
        private void loadDigitalControls()
        {
            //add controls and state change indicators/buttons for each digital pin the board supports
            for( byte i = 0; i < numberOfDigitalPins; ++i )
            {
                var stack = new StackPanel();
                stack.Orientation = Orientation.Horizontal;
                stack.FlowDirection = FlowDirection.LeftToRight;

                //set up the mode toggle button
                var button = new Button();
                var image = new Image();
                image.Stretch = Stretch.Uniform;
                digitalModeImages.Add( i, image );
                button.Content = image;
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.VerticalAlignment = VerticalAlignment.Center;
                button.Padding = new Thickness(); ;
                button.Margin = new Thickness( 5, 0, 5, 0 ); ;
                button.Name = "digitalmode_" + i;
                button.Click += OnClick_DigitalModeToggleButton;
                stack.Children.Add( button );

                //set up the state toggle indicator/button
                button = new Button();
                image = new Image();
                image.Stretch = Stretch.Uniform;
                digitalStateImages.Add( i, image );
                button.Content = image;
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.VerticalAlignment = VerticalAlignment.Center;
                button.Padding = new Thickness();
                button.Margin = new Thickness( 5, 0, 5, 0 );
                button.Name = "digitalstate_" + i;
                button.Click += OnClick_DigitalStateToggleButton;
                stack.Children.Add( button );

                DigitalControls.Children.Add( stack );
            }
        }


        /// <summary>
        /// Adds the necessary i2c controls to the i2c pivot page, this will only be called the first time this pivot page is loaded
        /// </summary>
        private void loadI2cControls()
        {
            var stack = new StackPanel();
            stack.Orientation = Orientation.Horizontal;
            stack.FlowDirection = FlowDirection.LeftToRight;
        }

        /// <summary>
        /// This function will determine which indicator image should be applied for a given digital pin and apply it to the correct Image object
        /// </summary>
        /// <param name="pin">the pin number to be updated</param>
        private void UpdateDigitalPinStateIndicator( byte pin )
        {
            if( !digitalStateImages.ContainsKey( pin ) ) return;

            ImageSource image;
            if( arduino.getPinMode( pin ) == PinMode.PWM ) image = bitmaps["analog"];
            else if( arduino.digitalRead( pin ) == PinState.HIGH ) image = bitmaps["high"];
            else image = bitmaps["low"];

            digitalStateImages[pin].Source = image;
        }

        /// <summary>
        /// This function will determine which pin mode image should be applied for a given digital pin and apply it to the correct Image object
        /// </summary>
        /// <param name="pin">the pin number to be updated</param>
        private void UpdateDigitalPinModeIndicator( byte pin )
        {
            if( !digitalModeImages.ContainsKey( pin ) ) return;

            ImageSource image;
            switch( arduino.getPinMode( pin ) )
            {
                case PinMode.INPUT:
                    image = bitmaps["input_" + pin];
                    break;

                case PinMode.OUTPUT:
                    image = bitmaps["output_" + pin];
                    break;

                default:
                case PinMode.PWM:
                    image = bitmaps["disabled_" + pin];
                    break;
            }

            digitalModeImages[pin].Source = image;
        }

        /// <summary>
        /// This function will determine which pin mode image should be applied for a given analog pin and apply it to the correct Image object
        /// </summary>
        /// <param name="pin">the pin number to be updated</param>
        private void UpdateAnalogPinModeIndicator( byte pin )
        {
            if( !analogModeImages.ContainsKey( pin ) ) return;

            ImageSource image;
            var analogPinNumber = ConvertAnalogPinToPinNumber( pin );

            if( isI2cEnabled && ( analogPinNumber == i2cPins[0] || analogPinNumber == i2cPins[1] ) )
            {
                image = bitmaps["disabled_a" + pin];
            }
            else
                switch( arduino.getPinMode( analogPinNumber ) )
                {
                    case PinMode.ANALOG:
                        image = bitmaps["input_a" + pin];
                        break;

                    case PinMode.I2C:
                        image = bitmaps["disabled_a" + pin];
                        break;

                    default:
                        image = bitmaps["none_a" + pin];
                        break;
                }

            analogModeImages[pin].Source = image;
        }

        /// <summary>
        /// This function will apply the given value to the given analog pin input
        /// </summary>
        /// <param name="pin">the pin number to be updated</param>
        /// <param name="value">the value to display</param>
        private void UpdateAnalogTextBlockValue( byte pin, ushort value )
        {
            if( analogTextBlocks.ContainsKey( pin ) ) analogTextBlocks[pin].Text = Convert.ToString( value );
        }

        /// <summary>
        /// This function will determine which pin mode image should be applied for a given pwm pin and apply it to the correct Image object
        /// </summary>
        /// <param name="pin">the pin number to be updated</param>
        private void UpdatePwmPinModeIndicator( byte pin )
        {
            if( !pwmModeImages.ContainsKey( pin ) ) return;

            ImageSource image;
            switch( arduino.getPinMode( pin ) )
            {
                case PinMode.PWM:
                    image = bitmaps["output_" + pin];
                    break;

                default:
                    image = bitmaps["disabled_" + pin];
                    pwmSliders[pin].Visibility = Visibility.Collapsed;
                    break;
            }

            pwmModeImages[pin].Source = image;
        }

        /// <summary>
        /// displays a toast with the given heading, body, and optional second body
        /// </summary>
        /// <param name="heading">A required heading</param>
        /// <param name="body">A required body</param>
        /// <param name="body2">an optional second body</param>
        private void ShowToast( string heading, string body, string body2 )
        {
            var builder = new StringBuilder();
            builder.Append( "<toast><visual version='1'><binding template='ToastText04'><text id='1'>" )
                .Append( heading )
                .Append( "</text><text id='2'>" )
                .Append( body )
                .Append( "</text>" );

            if( !string.IsNullOrEmpty( body2 ) )
            {
                builder.Append( "<text id='3'>" )
                    .Append( body2 )
                    .Append( "</text>" );
            }

            builder.Append( "</binding>" )
                .Append( "</visual>" )
                .Append( "</toast>" );

            var toastDom = new Windows.Data.Xml.Dom.XmlDocument();
            toastDom.LoadXml( builder.ToString() );
            var toast = new ToastNotification( toastDom );
            ToastNotificationManager.CreateToastNotifier().Show( toast );
        }


        //******************************************************************************
        //* Utility Functions
        //******************************************************************************

        /// <summary>
        /// retrieves the pin number associated with a button object
        /// </summary>
        /// <param name="button">the button to retrieve a pin number from</param>
        /// <returns>the pin number</returns>
        private byte GetPinFromButtonObject( Button button )
        {
            return Convert.ToByte( button.Name.Substring( button.Name.IndexOf( '_' ) + 1 ) );
        }

        /// <summary>
        /// Parses a string to retrieve an int value. Strings may be in hex (0x??)
        /// binary (0b????????) or decimal. Leading 0 not necessary.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private uint ParsePositiveDecimalValueOrThrow( string text )
        {
            if( string.IsNullOrEmpty( text ) ) throw new FormatException();

            int val;

            //did they enter a number in binary or hex format?
            if( text.Contains( "x" ) )
            {
                val = Convert.ToInt32( text.Substring( text.IndexOf( "x" ) + 1 ), 16 );
            }
            else if( text.Contains( "b" ) )
            {
                val = Convert.ToInt32( text.Substring( text.IndexOf( 'b' ) + 1 ), 2 );
            }
            else
            {
                val = Convert.ToInt32( text );
            }

            if( val < 0 ) throw new FormatException();

            return (uint)val;
        }


        /// <summary>
        /// Arduino numbers their analog pins directly after the digital pins. Meaning A0 is actally pin 14 on an Uno,
        /// because there are 14 digital pins on an Uno. Therefore, when we're working with functions that don't know the
        /// difference between Analog and Digital pin numbers, we need to convert pin 0 (meaning A0) into pin + numberOfDigitalPins
        /// </summary>
        /// <param name="pin"></param>
        /// <returns></returns>
        private byte ConvertAnalogPinToPinNumber( byte pin )
        {
            return (byte)( pin + numberOfDigitalPins );
        }


        /// <summary>
        /// Verifies that the address and register text fields are valid and parses the values if so.
        /// </summary>
        /// <param name="address">the address variable to be updated</param>
        /// <param name="register">the register variable to be updated</param>
        /// <returns></returns>
        private bool VerifyAddressAndRegister( out uint address, out uint register )
        {
            if( string.IsNullOrEmpty( I2cAddressTextBox.Text ) || string.IsNullOrEmpty( I2cRegisterTextBox.Text ) )
            {
                ShowToast( "Nothing sent.", "You must specify an address and register.", null );
                address = 0;
                register = 0;
                return false;
            }

            address = ParsePositiveDecimalValueOrThrow( I2cAddressTextBox.Text );
            register = ParsePositiveDecimalValueOrThrow( I2cRegisterTextBox.Text );
            if( address > 255 || register > 255 )
            {
                ShowToast( "Value too large.", "Byte values cannot be larger than 255", null );
                address = 0;
                register = 0;
                return false;
            }

            return true;
        }
    }
}
