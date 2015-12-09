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
using Windows.UI.Xaml.Input;
using Windows.UI.Text;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace remote_wiring_experience
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /*
         * we want to programatically create our UI using RetrieveDeviceConfiguration so that we can support any Arduino type
         */
        private List<byte> disabledPins;
        private List<byte> analogPins;
        private List<byte> digitalPins;
        private List<byte> pwmPins;
        private List<byte> i2cPins;
        private int analogOffset;
        private bool isI2cEnabled = false;

        //stores image assets so that they can be loaded once and reused many times
        private Dictionary<string, BitmapImage> bitmaps;

        //these dictionaries store the loaded UI elements for easy access by pin number
        private Dictionary<byte, ToggleSwitch> digitalModeToggleSwitches;
        private Dictionary<byte, ToggleSwitch> digitalStateToggleSwitches;
        private Dictionary<byte, TextBlock> digitalStateTextBlocks;
        private Dictionary<byte, ToggleSwitch> analogModeToggleSwitches;
        private Dictionary<byte, Slider> analogSliders;
        private Dictionary<byte, TextBlock> analogTextBlocks;
        private Dictionary<byte, ToggleSwitch> pwmModeToggleSwitches;
        private Dictionary<byte, TextBlock> pwmTextBlocks;
        private Dictionary<byte, Slider> pwmSliders;
        
        private RemoteDevice arduino;

        //telemetry-related items
        DateTime lastPageNavigationTime;

        private int currentPage = 0;
        private String[] pages = { "Digital", "Analog", "PWM", "About" };
        private bool navigated = false;
        private bool resetVoltage = false;

        public MainPage()
        {
            this.InitializeComponent();

            //retrieve the remote device configuration from Remote Arduino
            RetrieveDeviceConfiguration();

            //UI Elements dictionaries
            bitmaps = new Dictionary<string, BitmapImage>();
            digitalModeToggleSwitches = new Dictionary<byte, ToggleSwitch>();
            digitalStateToggleSwitches = new Dictionary<byte, ToggleSwitch>();
            digitalStateTextBlocks = new Dictionary<byte, TextBlock>();
            analogModeToggleSwitches = new Dictionary<byte, ToggleSwitch>();
            analogSliders = new Dictionary<byte, Slider>();
            analogTextBlocks = new Dictionary<byte, TextBlock>();
            pwmModeToggleSwitches = new Dictionary<byte, ToggleSwitch>();
            pwmTextBlocks = new Dictionary<byte, TextBlock>();
            pwmSliders = new Dictionary<byte, Slider>();
        }

        protected override void OnNavigatedTo( NavigationEventArgs e )
        {
            base.OnNavigatedTo( e );
            //LoadAssets();
            LoadPinPages();
            arduino = App.Arduino;
            arduino.DigitalPinUpdated += Arduino_OnDigitalPinUpdated;
            arduino.AnalogPinUpdated += Arduino_OnAnalogPinUpdated;

            for (byte i = 0; i < digitalPins.Count; ++i)
            {
                UpdateDigitalPinIndicators( digitalPins[i] );
            }

            App.Telemetry.TrackPageView("Digital_Controls_Page");
            lastPageNavigationTime = DateTime.Now;
        }


        //******************************************************************************
        //* Windows Remote Arduino callbacks
        //******************************************************************************

        /// <summary>
        /// This function is called when the Windows Remote Arduino library reports that an input value has changed for an analog pin.
        /// </summary>
        /// <param name="pin">The pin whose value has changed</param>
        /// <param name="value">the new value of the pin</param>
        private void Arduino_OnAnalogPinUpdated( byte pin, ushort value )
        {
            //we must dispatch the change to the UI thread to update the text field.
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                UpdateAnalogValueIndicator( pin, value );
                UpdatePwmPinModeIndicator(pin);
            } ) );
        }


        /// <summary>
        /// This function is called when the Windows Remote Arduino library reports that an input value has changed for a digital pin.
        /// </summary>
        /// <param name="pin">The pin whose value has changed</param>
        /// <param name="state">the new state of the pin, either HIGH or LOW</param>
        private void Arduino_OnDigitalPinUpdated( byte pin, PinState state )
        {
            //we must dispatch the change to the UI thread to change the indicator image
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                UpdateDigitalPinIndicators( pin );
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
        private void OnClick_DigitalModeToggleSwitch( object sender, RoutedEventArgs args )
        {
            // This bool fixes the bug where voltage returns to 0v after PWM but the slider still represents 5v.
            // Needed because switching from PWM to input to output automatically sets the pin to 0v.
            if (!navigated)
            {
                var button = sender as ToggleSwitch;
                var pin = GetPinFromButtonObject(button);

                //pins 0 and 1 are the serial pins and are in use. this manual check will show them as disabled
                if (pin == 0 || pin == 1)
                {
                    ShowToast("Pin unavailable.", "That pin is in use as a serial pin and cannot be used.", null);
                    return;
                }

                var mode = arduino.getPinMode(pin);
                var nextMode = (mode == PinMode.OUTPUT) ? PinMode.INPUT : PinMode.OUTPUT;

                // Fixes bug where voltage returns to 0v after pin input but slider still represents 5v.
                // Needed because switching to output mode automatically sets pin to 0v.
                resetVoltage = true;
                if (nextMode == PinMode.OUTPUT)
                {
                    digitalStateToggleSwitches[pin].IsOn = false;
                }
                resetVoltage = false;

                arduino.pinMode(pin, nextMode);

                //telemetry
                var properties = new Dictionary<string, string>();
                properties.Add("pin_number", pin.ToString());
                properties.Add("new_mode", nextMode.ToString());
                App.Telemetry.TrackEvent("Digital_Mode_Toggle_Button_Pressed", properties);

                UpdateDigitalPinIndicators(pin);
            }
        }

        /// <summary>
        /// Invoked when the digital state toggle button is tapped or pressed
        /// </summary>
        /// <param name="sender">the button being pressed</param>
        /// <param name="args">button press event args</param>
        private void OnClick_DigitalStateToggleSwitch( object sender, RoutedEventArgs args )
        {
            if (!resetVoltage)
            {
                var button = sender as ToggleSwitch;
                var pin = GetPinFromButtonObject(button);

                //pins 0 and 1 are the serial pins and are in use. this manual check will show them as disabled
                if (pin == 0 || pin == 1)
                {
                    ShowToast("Pin unavailable.", "That pin is in use as a serial pin and cannot be used.", null);
                    return;
                }

                if (arduino.getPinMode(pin) != PinMode.OUTPUT)
                {
                    ShowToast("Incorrect PinMode!", "You must first set this pin to OUTPUT.", null);
                    return;
                }

                var state = arduino.digitalRead(pin);
                var nextState = (state == PinState.HIGH) ? PinState.LOW : PinState.HIGH;

                arduino.digitalWrite(pin, nextState);

                //telemetry
                var properties = new Dictionary<string, string>();
                properties.Add("pin_number", pin.ToString());
                properties.Add("new_state", nextState.ToString());
                App.Telemetry.TrackEvent("Digital_State_Toggle_Button_Pressed", properties);

                UpdateDigitalPinIndicators(pin);
            }
        }


        /// <summary>
        /// Invoked when the analog mode toggle button is tapped or pressed
        /// </summary>
        /// <param name="sender">the button being pressed</param>
        /// <param name="args">button press event args</param>
        private void OnClick_AnalogModeToggleSwitch(object sender, RoutedEventArgs args)
        {
            var button = sender as ToggleSwitch;
            var pin = GetPinFromButtonObject(button);
            var analogPinNumber = ConvertAnalogPinToPinNumber(pin);

            //var mode = arduino.getPinMode(analogPinNumber);
            var mode = arduino.getPinMode("A" + pin);
            var nextMode = (mode == PinMode.OUTPUT) ? PinMode.ANALOG : PinMode.OUTPUT;

            arduino.pinMode("A" + pin, nextMode);

            //telemetry
            var properties = new Dictionary<string, string>();
            properties.Add("pin_number", pin.ToString());
            properties.Add("new_mode", nextMode.ToString());
            App.Telemetry.TrackEvent("Analog_Mode_Toggle_Button_Pressed", properties);

            UpdateAnalogPinModeIndicator(pin);
        }

        /// <summary>
        /// Invoked when the pwm mode toggle button is tapped or pressed
        /// </summary>
        /// <param name="sender">the button being pressed</param>
        /// <param name="args">button press event args</param>
        private void OnClick_PwmModeToggleSwitch(object sender, RoutedEventArgs args)
        {
            var button = sender as ToggleSwitch;
            var pin = GetPinFromButtonObject(button);

            var mode = arduino.getPinMode(pin);
            var nextMode = (mode == PinMode.PWM) ? PinMode.OUTPUT : PinMode.PWM;

            resetVoltage = true;
            if (nextMode == PinMode.OUTPUT)
            {
                digitalStateToggleSwitches[pin].IsOn = false;
            }
            resetVoltage = false;

            //telemetry
            var properties = new Dictionary<string, string>();
            properties.Add("pin_number", pin.ToString());
            properties.Add("new_state", nextMode.ToString());
            App.Telemetry.TrackEvent("Pwm_Mode_Toggle_Button_Pressed", properties);

            arduino.pinMode(pin, nextMode);
            UpdatePwmPinModeIndicator(pin);
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

            //pwmTextBlocks[pin].Text = args.NewValue.ToString();
            arduino.analogWrite( pin, (byte)args.NewValue );
        }

        /// <summary>
        /// This function helps to process telemetry events when manipulation of a PWM slider is complete, 
        /// rather than after each tick.
        /// </summary>
        /// <param name="sender">the slider which was released</param>
        /// <param name="args">the slider release event args</param>
        private void OnPointerReleased_PwmSlider( object sender, PointerRoutedEventArgs args )
        {
            var slider = sender as Slider;
            var pin = Convert.ToByte( slider.Name.Substring( slider.Name.IndexOf( '_' ) + 1 ) );

            var properties = new Dictionary<string, string>();
            properties.Add( "pin_number", pin.ToString() );
            properties.Add( "analog_value", slider.Value.ToString() );
            App.Telemetry.TrackEvent( "Pwm_Slider_Value_Changed", properties );
        }


        //******************************************************************************
        //* UI Support Functions
        //******************************************************************************

        /// <summary>
        /// This function loads the content of each of the pin pages, as well as the About page.  The reason this is done dynamically here, instead of statically in the XAML, is to leave the code open
        /// to the possibility of dynamically filling the pages based on the specific pin numbers/orientations of the connected board.
        /// </summary>
        private void LoadPinPages()
        {
            // Load the Digital page content.
            loadDigitalControls();
            loadAnalogControls();
            loadPWMControls();
        }


        /// <summary>
        /// Adds the necessary digital controls to a StackPanel created for the Digital page.  This will only be called on navigation from the Connections page.
        /// </summary>
        private void loadDigitalControls()
        {
            //add controls and state change indicators/buttons for each digital pin and disabled pin the board supports
            for (byte i = 0; i < digitalPins.Count; ++i)
            {
                bool isPinDisabled = disabledPins.Contains( digitalPins[i] );

                // Container stack to hold all pieces of new row of pins.
                var containerStack = new StackPanel();
                containerStack.Orientation = Orientation.Horizontal;
                containerStack.FlowDirection = FlowDirection.LeftToRight;
                containerStack.HorizontalAlignment = HorizontalAlignment.Stretch;
                containerStack.Margin = new Thickness(8, 0, 0, 20);

                // Set up the pin text.
                var textStack = new StackPanel();
                textStack.Orientation = Orientation.Vertical;
                textStack.FlowDirection = FlowDirection.LeftToRight;
                textStack.HorizontalAlignment = HorizontalAlignment.Stretch;

                var text = new TextBlock();
                text.HorizontalAlignment = HorizontalAlignment.Stretch;
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Margin = new Thickness(0, 0, 0, 0);
                text.Text = "Pin " + ( analogPins.Contains( digitalPins[i] ) ? "A" + ( digitalPins[i] - analogOffset ) : "" + digitalPins[i] );
                text.FontSize = 14;
                text.FontWeight = FontWeights.SemiBold;

                var text2 = new TextBlock();
                text2.HorizontalAlignment = HorizontalAlignment.Stretch;
                text2.VerticalAlignment = VerticalAlignment.Center;
                text2.Margin = new Thickness(0, 0, 0, 0);
                text2.Text = "Digital";
                text2.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 106, 107, 106));
                text2.FontSize = 14;
                text2.FontWeight = FontWeights.SemiBold;

                textStack.Children.Add(text);
                textStack.Children.Add(text2);
                containerStack.Children.Add(textStack);

                // Set up the mode toggle button.
                var modeStack = new StackPanel();
                modeStack.Orientation = Orientation.Horizontal;
                modeStack.FlowDirection = FlowDirection.LeftToRight;
                modeStack.HorizontalAlignment = HorizontalAlignment.Stretch;
                modeStack.Margin = new Thickness(92, 0, 0, 0);

                var toggleSwitch = new ToggleSwitch();
                toggleSwitch.HorizontalAlignment = HorizontalAlignment.Left;
                toggleSwitch.VerticalAlignment = VerticalAlignment.Center;
                toggleSwitch.Margin = new Thickness(5, 0, 5, 0);
                toggleSwitch.Name = "digitalmode_" + i;
                toggleSwitch.Toggled += OnClick_DigitalModeToggleSwitch;
                toggleSwitch.IsEnabled = !isPinDisabled;

                var onContent = new TextBlock();
                onContent.Text = "Input";
                onContent.FontSize = 14;
                toggleSwitch.OnContent = onContent;
                var offContent = new TextBlock();
                offContent.Text = ( isPinDisabled ? "Disabled" : "Output" );
                offContent.FontSize = 14;
                toggleSwitch.OffContent = offContent;
                digitalModeToggleSwitches.Add(i, toggleSwitch);

                modeStack.Children.Add(toggleSwitch);
                containerStack.Children.Add(modeStack);

                // Set up the state toggle button.
                var stateStack = new StackPanel();
                stateStack.Orientation = Orientation.Horizontal;
                stateStack.FlowDirection = FlowDirection.LeftToRight;
                stateStack.HorizontalAlignment = HorizontalAlignment.Stretch;

                var toggleSwitch2 = new ToggleSwitch();
                toggleSwitch2.HorizontalAlignment = HorizontalAlignment.Left;
                toggleSwitch2.VerticalAlignment = VerticalAlignment.Center;
                toggleSwitch2.Margin = new Thickness(1, 0, 5, 0);
                toggleSwitch2.Name = "digitalstate_" + i;
                toggleSwitch2.Toggled += OnClick_DigitalStateToggleSwitch;
                toggleSwitch2.IsEnabled = !isPinDisabled;

                var onContent2 = new TextBlock();
                onContent2.Text = "5v";
                onContent2.FontSize = 14;
                toggleSwitch2.OnContent = onContent2;
                var offContent2 = new TextBlock();
                offContent2.Text = ( isPinDisabled ? "Disabled" : "0v" );
                offContent2.FontSize = 14;
                toggleSwitch2.OffContent = offContent2;
                digitalStateToggleSwitches.Add(i, toggleSwitch2);

                var text3 = new TextBlock();
                text3.HorizontalAlignment = HorizontalAlignment.Stretch;
                text3.VerticalAlignment = VerticalAlignment.Center;
                text3.Margin = new Thickness(0, 0, 0, 0);
                if( isPinDisabled )
                {
                    text3.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 106, 107, 106));
                    text3.Text = "Disabled for serial connection or other use.";
                }
                else
                {
                    text3.Text = "0v";
                }
                text3.FontSize = 14;
                text3.Visibility = Visibility.Collapsed;
                digitalStateTextBlocks.Add(i, text3);

                stateStack.Children.Add(text3);
                stateStack.Children.Add(toggleSwitch2);
                containerStack.Children.Add(stateStack);

                // Add entire row to page.
                DigitalPins.Children.Add(containerStack);
            }
        }

        /// <summary>
        /// Adds the necessary analog controls to a StackPanel created for the Analog page. This will only be called on navigation from the Connections page.
        /// </summary>
        private void loadAnalogControls()
        {
            //add controls and text fields for each analog pin the board supports
            for( byte i = 0; i < analogPins.Count; ++i )
            {
                // Container stack to hold all pieces of new row of pins.
                var containerStack = new StackPanel();
                containerStack.Orientation = Orientation.Horizontal;
                containerStack.FlowDirection = FlowDirection.LeftToRight;
                containerStack.HorizontalAlignment = HorizontalAlignment.Stretch;
                containerStack.Margin = new Thickness(8, 0, 0, 20);

                // Set up the pin text.
                var textStack = new StackPanel();
                textStack.Orientation = Orientation.Vertical;
                textStack.FlowDirection = FlowDirection.LeftToRight;
                textStack.HorizontalAlignment = HorizontalAlignment.Stretch;

                var text = new TextBlock();
                text.HorizontalAlignment = HorizontalAlignment.Stretch;
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Margin = new Thickness(0, 0, 0, 0);
                text.Text = "Pin A" + ( analogPins[i] - analogOffset );
                text.FontSize = 14;
                text.FontWeight = FontWeights.SemiBold;

                var text2 = new TextBlock();
                text2.HorizontalAlignment = HorizontalAlignment.Stretch;
                text2.VerticalAlignment = VerticalAlignment.Center;
                text2.Margin = new Thickness(0, 0, 0, 0);
                text2.Text = "Analog";
                text2.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 106, 107, 106));
                text2.FontSize = 14;
                text2.FontWeight = FontWeights.SemiBold;

                textStack.Children.Add(text);
                textStack.Children.Add(text2);
                containerStack.Children.Add(textStack);

                // Set up the mode toggle button.
                var modeStack = new StackPanel();
                modeStack.Orientation = Orientation.Horizontal;
                modeStack.FlowDirection = FlowDirection.LeftToRight;
                modeStack.HorizontalAlignment = HorizontalAlignment.Stretch;
                modeStack.Margin = new Thickness(88, 0, 0, 0);

                var toggleSwitch = new ToggleSwitch();
                toggleSwitch.HorizontalAlignment = HorizontalAlignment.Left;
                toggleSwitch.VerticalAlignment = VerticalAlignment.Center;
                toggleSwitch.Margin = new Thickness(5, 0, 5, 0);
                toggleSwitch.Name = "analogmode_" + i;
                toggleSwitch.Toggled += OnClick_AnalogModeToggleSwitch;

                var onContent = new TextBlock();
                onContent.Text = "Input";
                onContent.FontSize = 14;
                toggleSwitch.OnContent = onContent;
                var offContent = new TextBlock();
                offContent.Text = "Output";
                offContent.FontSize = 14;
                toggleSwitch.OffContent = offContent;
                analogModeToggleSwitches.Add(i, toggleSwitch);

                modeStack.Children.Add(toggleSwitch);
                containerStack.Children.Add(modeStack);

                //set up the indication text
                var text3 = new TextBlock();
                text3.HorizontalAlignment = HorizontalAlignment.Stretch;
                text3.VerticalAlignment = VerticalAlignment.Center;
                text3.Margin = new Thickness( 2, 0, 0, 0 );
                text3.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 106, 107, 106));
                text3.Text = "Cannot write to analog pins.";
                text3.FontSize = 14;
                analogTextBlocks.Add( i, text3 );
                containerStack.Children.Add( text3 );

                AnalogPins.Children.Add( containerStack );
            }
        }


        /// <summary>
        /// Adds the necessary analog controls to a StackPanel created for the PWM page. This will only be called on navigation from the Connections page.
        /// </summary>
        private void loadPWMControls()
        {
            //add controls and value sliders for each pwm pin the board supports
            for (byte i = 0; i < pwmPins.Count; ++i)
            {
                // Container stack to hold all pieces of new row of pins.
                var containerStack = new StackPanel();
                containerStack.Orientation = Orientation.Horizontal;
                containerStack.FlowDirection = FlowDirection.LeftToRight;
                containerStack.HorizontalAlignment = HorizontalAlignment.Stretch;
                containerStack.Margin = new Thickness(8, 0, 0, 20);

                // Set up the pin text.
                var textStack = new StackPanel();
                textStack.Orientation = Orientation.Vertical;
                textStack.FlowDirection = FlowDirection.LeftToRight;
                textStack.HorizontalAlignment = HorizontalAlignment.Stretch;

                var text = new TextBlock();
                text.HorizontalAlignment = HorizontalAlignment.Stretch;
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Margin = new Thickness(0, 0, 0, 0);
                text.Text = "Pin " + pwmPins[i];
                text.FontSize = 14;
                text.FontWeight = FontWeights.SemiBold;

                var text2 = new TextBlock();
                text2.HorizontalAlignment = HorizontalAlignment.Stretch;
                text2.VerticalAlignment = VerticalAlignment.Center;
                text2.Margin = new Thickness(0, 0, 0, 0);
                text2.Text = "PWM";
                text2.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 106, 107, 106));
                text2.FontSize = 14;
                text2.FontWeight = FontWeights.SemiBold;

                textStack.Children.Add(text);
                textStack.Children.Add(text2);
                containerStack.Children.Add(textStack);

                // Set up the mode toggle button.
                var modeStack = new StackPanel();
                modeStack.Orientation = Orientation.Horizontal;
                modeStack.FlowDirection = FlowDirection.LeftToRight;
                modeStack.HorizontalAlignment = HorizontalAlignment.Stretch;
                modeStack.Margin = new Thickness(88, 0, 0, 0);

                var toggleSwitch = new ToggleSwitch();
                toggleSwitch.HorizontalAlignment = HorizontalAlignment.Left;
                toggleSwitch.VerticalAlignment = VerticalAlignment.Center;
                if (pwmPins[i] == 10 || pwmPins[i] == 13) { toggleSwitch.Margin = new Thickness(13, 0, 5, 0); }
                else { toggleSwitch.Margin = new Thickness(15, 0, 5, 0); }
                toggleSwitch.Name = "pwmmode_" + pwmPins[i];
                toggleSwitch.Toggled += OnClick_PwmModeToggleSwitch;

                var onContent = new TextBlock();
                onContent.Text = "Enabled";
                onContent.FontSize = 14;
                toggleSwitch.OnContent = onContent;
                var offContent = new TextBlock();
                offContent.Text = "Disabled";
                offContent.FontSize = 14;
                toggleSwitch.OffContent = offContent;
                pwmModeToggleSwitches.Add(pwmPins[i], toggleSwitch);

                modeStack.Children.Add(toggleSwitch);
                containerStack.Children.Add(modeStack);

                //set up the value change slider
                var slider = new Slider();
                slider.Visibility = Visibility.Collapsed;
                slider.Orientation = Orientation.Horizontal;
                slider.HorizontalAlignment = HorizontalAlignment.Stretch;
                slider.SmallChange = 32;
                slider.StepFrequency = 32;
                slider.TickFrequency = 32;
                slider.ValueChanged += OnValueChanged_PwmSlider;
                slider.PointerReleased += OnPointerReleased_PwmSlider;
                slider.Minimum = 0;
                slider.Maximum = 255;
                slider.Name = "pwmslider_" + pwmPins[i];
                slider.Width = 180;
                slider.Height = 34;
                slider.Margin = new Thickness(3, 0, 0, 0);
                pwmSliders.Add(pwmPins[i], slider);
                containerStack.Children.Add(slider);

                //set up the indication text
                var text3 = new TextBlock();
                text3.HorizontalAlignment = HorizontalAlignment.Stretch;
                text3.VerticalAlignment = VerticalAlignment.Center;
                text3.Margin = new Thickness(3, 0, 0, 0);
                text3.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 106, 107, 106));
                text3.Text = "Enable PWM to write values.";
                text3.FontSize = 14;
                text3.Name = "pwmtext_" + pwmPins[i];
                text3.Visibility = Visibility.Visible;
                pwmTextBlocks.Add(pwmPins[i], text3);
                containerStack.Children.Add(text3);

                PWMPins.Children.Add(containerStack);
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
        /// This function will determine which pin mode image should be applied for a given digital pin and apply it to the correct Image object
        /// </summary>
        /// <param name="pin">the pin number to be updated</param>
        private void UpdateDigitalPinIndicators( byte pin )
        {
            if (!digitalModeToggleSwitches.ContainsKey(pin)) return;
            
            if( disabledPins.Contains( pin ) )
            {
                digitalModeToggleSwitches[pin].IsEnabled = false;
                digitalStateToggleSwitches[pin].IsEnabled = false;
                digitalStateToggleSwitches[pin].Visibility = Visibility.Collapsed;
                digitalStateTextBlocks[pin].Visibility = Visibility.Visible;
            }
            else
            {
                PinMode mode = arduino.getPinMode( pin );
                bool applyUsageMessage = false;
                switch( mode )
                {
                    case PinMode.INPUT:
                        digitalModeToggleSwitches[pin].IsEnabled = true;
                        digitalModeToggleSwitches[pin].IsOn = true;
                        navigated = false;
                        digitalStateToggleSwitches[pin].IsEnabled = true;
                        digitalStateToggleSwitches[pin].Visibility = Visibility.Collapsed;
                        digitalStateTextBlocks[pin].Foreground = new SolidColorBrush( Windows.UI.Color.FromArgb( 255, 0, 0, 0 ) );
                        digitalStateTextBlocks[pin].Text = ( ( arduino.digitalRead( pin ) ) == PinState.HIGH ) ? "5v" : "0v";
                        digitalStateTextBlocks[pin].Visibility = Visibility.Visible;
                        break;

                    case PinMode.OUTPUT:
                        digitalModeToggleSwitches[pin].IsEnabled = true;
                        digitalModeToggleSwitches[pin].IsOn = false;
                        navigated = false;
                        digitalStateToggleSwitches[pin].IsEnabled = true;
                        digitalStateToggleSwitches[pin].Visibility = Visibility.Visible;
                        digitalStateTextBlocks[pin].Visibility = Visibility.Collapsed;
                        break;
                        
                    default:
                        applyUsageMessage = true;
                        digitalModeToggleSwitches[pin].IsEnabled = false;
                        digitalStateToggleSwitches[pin].IsEnabled = false;
                        digitalStateToggleSwitches[pin].Visibility = Visibility.Collapsed;
                        digitalStateTextBlocks[pin].Foreground = new SolidColorBrush( Windows.UI.Color.FromArgb( 255, 106, 107, 106 ) );
                        digitalStateTextBlocks[pin].Visibility = Visibility.Visible;
                        break;
                }

                //PWM and ANALOG have the same UI config as 'default' in the switch above, but we want a custom message. Two switches reduce duplicate code.
                if( applyUsageMessage )
                {
                    switch( mode )
                    {
                        case PinMode.PWM:
                            digitalStateTextBlocks[pin].Text = "Disabled for PWM use.";
                            break;
                            
                        case PinMode.ANALOG:
                            digitalStateTextBlocks[pin].Text = "Disabled for Analog use.";
                            break;

                        default:
                            digitalStateTextBlocks[pin].Text = "Disabled for other use.";
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// This function will determine which pin mode image should be applied for a given analog pin and apply it to the correct Image object
        /// </summary>
        /// <param name="pin">the pin number to be updated</param>
        private void UpdateAnalogPinModeIndicator( byte pin )
        {
            if( !analogModeToggleSwitches.ContainsKey( pin ) ) return;

            var analogPinNumber = ConvertAnalogPinToPinNumber( pin );

            if( isI2cEnabled && ( analogPinNumber == i2cPins[0] || analogPinNumber == i2cPins[1] ) )
            {
                //analogSliders[pin].IsEnabled = false;
            }
            else
                switch( arduino.getPinMode( "A" + pin ) )
                {
                    case PinMode.ANALOG:
                        //analogSliders[pin].Visibility = Visibility.Collapsed;
                        analogTextBlocks[pin].Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
                        analogTextBlocks[pin].Text = "" + arduino.analogRead("A" + pin);
                        break;

                    case PinMode.I2C:
                        //analogSliders[pin].Visibility = Visibility.Collapsed;
                        break;

                    default:
                        //analogSliders[pin].Visibility = Visibility.Visible;
                        analogTextBlocks[pin].Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 106, 107, 106));
                        analogTextBlocks[pin].Text = "Cannot write to analog pins.";
                        break;
                }

        }

        /// <summary>
        /// This function will apply the given value to the given analog pin input
        /// </summary>
        /// <param name="pin">the pin number to be updated</param>
        /// <param name="value">the value to display</param>
        private void UpdateAnalogValueIndicator( byte pin, ushort value )
        {
            if( arduino.getPinMode( "A" + pin ) != PinMode.ANALOG ) return;
            if( analogTextBlocks.ContainsKey( pin ) ) analogTextBlocks[pin].Text = Convert.ToString( value );
        }

        /// <summary>
        /// This function will determine which pin mode image should be applied for a given pwm pin and apply it to the correct Image object
        /// </summary>
        /// <param name="pin">the pin number to be updated</param>
        private void UpdatePwmPinModeIndicator( byte pin )
        {
            if( !pwmModeToggleSwitches.ContainsKey( pin ) ) return;

            switch( arduino.getPinMode( pin ) )
            {
                case PinMode.PWM:
                    pwmSliders[pin].Visibility = Visibility.Visible;
                    pwmTextBlocks[pin].Visibility = Visibility.Collapsed;
                    break;

                default:
                    pwmSliders[pin].Visibility = Visibility.Collapsed;
                    pwmTextBlocks[pin].Visibility = Visibility.Visible;
                    break;
            }
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
            try
            {
                ToastNotificationManager.CreateToastNotifier().Show( toast );
            }
            catch( Exception )
            {
                //do nothing, toast will gracefully fail
            }
        }


        //******************************************************************************
        //* Utility Functions
        //******************************************************************************


        /// <summary>
        /// Arduino numbers their analog pins directly after the digital pins. Meaning A0 is actally pin 14 on an Uno,
        /// because there are 14 digital pins on an Uno. Therefore, when we're working with functions that don't know the
        /// difference between Analog and Digital pin numbers, we need to convert pin 0 (meaning A0) into pin + numberOfDigitalPins
        /// </summary>
        /// <param name="pin"></param>
        /// <returns></returns>
        private byte ConvertAnalogPinToPinNumber( byte pin )
        {
            return (byte)( pin + digitalPins.Count );
        }

        /// <summary>
        /// retrieves the pin number associated with a button object
        /// </summary>
        /// <param name="button">the button to retrieve a pin number from</param>
        /// <returns>the pin number</returns>
        private byte GetPinFromButtonObject( ToggleSwitch button )
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
            if( text.ToLower().Contains( "x" ) )
            {
                val = Convert.ToInt32( text.Substring( text.IndexOf( "x" ) + 1 ), 16 );
            }
            else if( text.ToLower().Contains( "b" ) )
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

        private void RetrieveDeviceConfiguration()
        {
            HardwareProfile hardware = App.Arduino.DeviceHardwareProfile;
            if( hardware == null )
            {
                //This app will never use unsafe mode, so the hardware profile should never be null
                throw new NullReferenceException( "RemoteDevice HardwareProfile is invalid" );
            }

            analogOffset = hardware.AnalogOffset;
            disabledPins = new List<byte>();
            analogPins = new List<byte>();
            digitalPins = new List<byte>();
            pwmPins = new List<byte>();
            i2cPins = new List<byte>();

            //HardwareProfile offers helper functions to determine if a pin has a single capability at a time,
            //however, we'll do this manually since we care about multiple capabilities & it will therefore be more performant
            for( byte pin = 0; pin < hardware.TotalPinCount; ++pin )
            {
                byte mask = hardware.getPinCapabilitiesBitmask( pin );

                //disabled pins are typically Serial pins & will be shown on the digital page
                if( mask == 0 )
                {
                    disabledPins.Add( pin );
                    digitalPins.Add( pin );
                    continue;
                }

                if( ( mask & (byte)PinCapability.ANALOG ) > 0 )
                {
                    analogPins.Add( pin );

                    //set the initial state to digital output, pinMode will do nothing if not supported
                    App.Arduino.pinMode( pin, PinMode.OUTPUT );
                }

                if( ( mask & (byte)PinCapability.INPUT ) > 0 || ( mask & (byte)PinCapability.OUTPUT ) > 0 )
                {
                    digitalPins.Add( pin );
                }

                if( ( mask & (byte)PinCapability.PWM ) > 0 )
                {
                    pwmPins.Add( pin );
                }

                if( ( mask & (byte)PinCapability.I2C ) > 0 )
                {
                    i2cPins.Add( pin );
                }
            }
        }

        /// <summary>
        /// This function sends a single Analog telemetry event
        /// </summary>
        /// <param name="pin">the pin number to be reported</param>
        /// <param name="value">the value of the pin</param>
        private void SendAnalogTelemetryEvent(byte pin, double value)
        {
            var properties = new Dictionary<string, string>();
            properties.Add("pin_number", pin.ToString());
            properties.Add("analog_value", value.ToString());
            App.Telemetry.TrackEvent("Analog_Slider_Value_Changed", properties);
        }


        //******************************************************************************
        //* Menu Button Click Events
        //******************************************************************************
        
        /// <summary>
        /// Called if the pointer hovers over any of the menu buttons.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void MenuButton_Enter( object sender, RoutedEventArgs e )
        {
            var button = sender as Button;
            switch( button.Name )
            {
                case "ConnectButton":
                    ConnectionRectangle.Visibility = Visibility.Visible;
                    break;

                case "DigitalButton":
                    DigitalRectangle.Visibility = Visibility.Visible;
                    break;

                case "AnalogButton":
                    AnalogRectangle.Visibility = Visibility.Visible;
                    break;

                case "PWMButton":
                    PWMRectangle.Visibility = Visibility.Visible;
                    break;

                case "AboutButton":
                    AboutRectangle.Visibility = Visibility.Visible;
                    break;
            }
        }

        /// <summary>
        /// Called when a menu button is pressed
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            ResetVisibility();

            int nextPage = 0;
            switch( button.Name )
            {
                case "ConnectButton":

                    ConnectionRectangle.Visibility = Visibility.Visible;
                    App.Telemetry.TrackMetric( "Page_" + pages[currentPage] + "_Time_Spent_In_Seconds", ( DateTime.Now - lastPageNavigationTime ).TotalSeconds );
                    lastPageNavigationTime = DateTime.Now;

                    this.Frame.Navigate( typeof( ConnectionPage ) );
                    return;

                case "DigitalButton":

                    //update menu and page visibility
                    DigitalText.Foreground = new SolidColorBrush( Windows.UI.Color.FromArgb( 255, 14, 127, 217 ) );
                    DigitalScroll.Visibility = Visibility.Visible;
                    DigitalRectangle.Visibility = Visibility.Visible;

                    //update digital indicators
                    for( byte pin = 0; pin < digitalPins.Count; ++pin )
                    {
                        navigated = true;
                        UpdateDigitalPinIndicators( pin );
                    }

                    App.Telemetry.TrackPageView( "Digital_Controls_Page" );
                    nextPage = 0;

                    break;

                case "AnalogButton":

                    //update menu and page visibility
                    AnalogScroll.Visibility = Visibility.Visible;
                    AnalogRectangle.Visibility = Visibility.Visible;
                    AnalogText.Foreground = new SolidColorBrush( Windows.UI.Color.FromArgb( 255, 14, 127, 217 ) );

                    //update analog indicators
                    for( byte i = 0; i < analogPins.Count; ++i )
                    {
                        UpdateAnalogPinModeIndicator( analogPins[i] );
                    }

                    App.Telemetry.TrackPageView( "Analog_Controls_Page" );
                    nextPage = 1;

                    break;

                case "PWMButton":

                    //update menu and page visibility
                    PWMScroll.Visibility = Visibility.Visible;
                    PWMRectangle.Visibility = Visibility.Visible;
                    PWMText.Foreground = new SolidColorBrush( Windows.UI.Color.FromArgb( 255, 14, 127, 217 ) );

                    //update PWM indicators
                    for( byte pin = 0; pin < pwmPins.Count; ++pin )
                    {
                        UpdatePwmPinModeIndicator( pin );
                    }

                    App.Telemetry.TrackPageView( "PWM_Controls_Page" );
                    nextPage = 2;

                    break;

                case "AboutButton":
                    AboutPanel.Visibility = Visibility.Visible;
                    AboutRectangle.Visibility = Visibility.Visible;
                    AboutText.Foreground = new SolidColorBrush( Windows.UI.Color.FromArgb( 255, 14, 127, 217 ) );

                    App.Telemetry.TrackPageView( "About_Page" );
                    nextPage = 3;
                    break;
            }

            //track time spent on page navigated away from
            navigated = false;
            App.Telemetry.TrackMetric( "Page_" + pages[currentPage] + "_Time_Spent_In_Seconds", ( DateTime.Now - lastPageNavigationTime ).TotalSeconds );
            lastPageNavigationTime = DateTime.Now;
            currentPage = nextPage;
        }

        /// <summary>
        /// Called if the pointer exits the boundaries of any button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void MenuButton_Exit(object sender, RoutedEventArgs e)
        {
            ConnectionRectangle.Visibility = Visibility.Collapsed;
            DigitalRectangle.Visibility = (currentPage == 0) ? Visibility.Visible : Visibility.Collapsed;
            AnalogRectangle.Visibility = (currentPage == 1) ? Visibility.Visible : Visibility.Collapsed;
            PWMRectangle.Visibility = (currentPage == 2) ? Visibility.Visible : Visibility.Collapsed;
            AboutRectangle.Visibility = (currentPage == 3) ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Resets the view of the menu to show nothing selected with no highlights
        /// </summary>
        private void ResetVisibility()
        {
            DigitalScroll.Visibility = Visibility.Collapsed;
            AnalogScroll.Visibility = Visibility.Collapsed;
            PWMScroll.Visibility = Visibility.Collapsed;
            AboutPanel.Visibility = Visibility.Collapsed;

            DigitalRectangle.Visibility = Visibility.Collapsed;
            AnalogRectangle.Visibility = Visibility.Collapsed;
            PWMRectangle.Visibility = Visibility.Collapsed;
            AboutRectangle.Visibility = Visibility.Collapsed;

            DigitalText.Foreground = new SolidColorBrush( Windows.UI.Colors.Black );
            AnalogText.Foreground = new SolidColorBrush( Windows.UI.Colors.Black );
            PWMText.Foreground = new SolidColorBrush( Windows.UI.Colors.Black );
            AboutText.Foreground = new SolidColorBrush( Windows.UI.Colors.Black );
        }
    }
}
