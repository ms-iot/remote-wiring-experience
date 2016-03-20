using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Communication;
using Microsoft.Maker.Serial;
using Microsoft.Maker.RemoteWiring;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace remote_wiring_experience
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConnectionPage : Page
    {
        DispatcherTimer timeout;
        // stopwatch for tracking connection timing
        Stopwatch connectionStopwatch = new Stopwatch();
        DateTime timePageNavigatedTo;
        CancellationTokenSource cancelTokenSource;

        BitmapImage wireBitmap;
        Image wire;

        bool navigated = false;

        public ConnectionPage()
        {
            this.InitializeComponent();
            ConnectionMethodComboBox.SelectionChanged += ConnectionComboBox_SelectionChanged;
        }

        protected override void OnNavigatedTo( NavigationEventArgs e )
        {
            base.OnNavigatedTo( e );

            navigated = true;
            Reset();
            navigated = false;

            //telemetry
            timePageNavigatedTo = DateTime.Now;

            // Load assets for wire icon
            wireBitmap = new BitmapImage(new Uri(BaseUri, @"Assets/wire.png"));
            wire = new Image();
            wire.Stretch = Stretch.Uniform;
            wire.Source = wireBitmap;
            WireStack.Children.Add(wire);

            if ( ConnectionList.ItemsSource == null )
            {
                ConnectMessage.Text = "Select an item to connect to.";
                RefreshDeviceList();
            }
        }

        private void RefreshDeviceList()
        {
            //invoke the listAvailableDevicesAsync method of the correct Serial class. Since it is Async, we will wrap it in a Task and add a llambda to execute when finished
            Task<DeviceInformationCollection> task = null;
            if( ConnectionMethodComboBox.SelectedItem == null )
            {
                ConnectMessage.Text = "Select a connection method to continue.";
                return;
            }

            switch( ConnectionMethodComboBox.SelectedItem as String )
            {
                default:
                case "Bluetooth":
                    ConnectionList.Visibility = Visibility.Visible;
                    DevicesText.Visibility = Visibility.Visible;
                    NetworkHostNameTextBox.IsEnabled = false;
                    NetworkPortTextBox.IsEnabled = false;
                    BaudRateComboBox.IsEnabled = true;
                    NetworkHostNameTextBox.Text = "";
                    NetworkPortTextBox.Text = "";

                    //create a cancellation token which can be used to cancel a task
                    cancelTokenSource = new CancellationTokenSource();
                    cancelTokenSource.Token.Register( () => OnConnectionCancelled() );

                    task = BluetoothSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>( cancelTokenSource.Token );
                    break;

                case "USB":
                    ConnectionList.Visibility = Visibility.Visible;
                    DevicesText.Visibility = Visibility.Visible;
                    NetworkHostNameTextBox.IsEnabled = false;
                    NetworkPortTextBox.IsEnabled = false;
                    BaudRateComboBox.IsEnabled = true;
                    NetworkHostNameTextBox.Text = "";
                    NetworkPortTextBox.Text = "";

                    //create a cancellation token which can be used to cancel a task
                    cancelTokenSource = new CancellationTokenSource();
                    cancelTokenSource.Token.Register( () => OnConnectionCancelled() );

                    task = UsbSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>( cancelTokenSource.Token );
                    break;

                case "Network":
                    ConnectionList.Visibility = Visibility.Collapsed;
                    DevicesText.Visibility = Visibility.Collapsed;
                    NetworkHostNameTextBox.IsEnabled = true;
                    NetworkPortTextBox.IsEnabled = true;
                    BaudRateComboBox.IsEnabled = false;
                    ConnectMessage.Text = "Enter a host and port to connect.";
                    task = null;
                    break;
            }

            if( task != null )
            {
                //store the returned DeviceInformation items when the task completes
                task.ContinueWith( listTask =>
                {
                    //store the result and populate the device list on the UI thread
                    var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
                    {
                        Connections connections = new Connections();

                        var result = listTask.Result;
                        if( result == null || result.Count == 0 )
                        {
                            ConnectMessage.Text = "No items found.";
                            ConnectionList.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            foreach( DeviceInformation device in result )
                            {
                                connections.Add( new Connection( device.Name, device ) );
                            }
                            ConnectMessage.Text = "Select an item and press \"Connect\" to connect.";
                        }

                        ConnectionList.ItemsSource = connections;
                    } ) );
                } );
            }
        }

        /****************************************************************
         *                       UI Callbacks                           *
         ****************************************************************/

        /// <summary>
        /// This function is called if the selection is changed on the Connection combo box
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void ConnectionComboBox_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            RefreshDeviceList();
        }

        /// <summary>
        /// Called if the Refresh button is pressed
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void RefreshButton_Click( object sender, RoutedEventArgs e )
        {
            RefreshDeviceList();
        }

        /// <summary>
        /// Called if the Cancel button is pressed
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void CancelButton_Click( object sender, RoutedEventArgs e )
        {
            OnConnectionCancelled();
        }

        /// <summary>
        /// Called if the Connect button is pressed
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void ConnectButton_Click( object sender, RoutedEventArgs e )
        {
            //disable the buttons and set a timer in case the connection times out
            SetUiEnabled( false );

            DeviceInformation device = null;
            if( ConnectionList.SelectedItem != null )
            {
                var selectedConnection = ConnectionList.SelectedItem as Connection;
                device = selectedConnection.Source as DeviceInformation;
            }
            else if( ( ConnectionMethodComboBox.SelectedItem as string ) != "Network" )
            {
                //if they haven't selected an item, but have chosen "usb" or "bluetooth", we can't proceed
                ConnectMessage.Text = "You must select an item to proceed.";
                SetUiEnabled( true );
                return;
            }

            //determine the selected baud rate
            uint baudRate = Convert.ToUInt32( ( BaudRateComboBox.SelectedItem as string ) );
            
            //use the selected device to create our communication object
            switch( ConnectionMethodComboBox.SelectedItem as string )
            {
                default:
                case "Bluetooth":

                    // populate telemetry properties about this connection attempt
                    App.Telemetry.Context.Properties["connection.name"] = String.Format("{0:X}", device.Name.GetHashCode());
                    App.Telemetry.Context.Properties["connection.detail"] = String.Format("{0:X}", device.Id.GetHashCode());

                    App.Connection = new BluetoothSerial( device );
                    break;

                case "USB":

                    // populate telemetry properties about this connection attempt
                    App.Telemetry.Context.Properties["connection.name"] = string.Format("{0:X}", device.Name.GetHashCode());
                    App.Telemetry.Context.Properties["connection.detail"] = string.Format("{0:X}", device.Id.GetHashCode());

                    App.Connection = new UsbSerial( device );
                    break;

                case "Network":
                    string host = NetworkHostNameTextBox.Text;
                    string port = NetworkPortTextBox.Text;
                    ushort portnum = 0;

                    if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
                    {
                        ConnectMessage.Text = "You have entered an invalid host or IP.";
                        return;
                    }

                    if (!ushort.TryParse(port, out portnum))
                    {
                        ConnectMessage.Text = "You have entered an invalid port number.";
                        return;
                    }

                    // populate telemetry properties about this connection attempt
                    App.Telemetry.Context.Properties["connection.name"] = string.Format("{0:X}", host.GetHashCode());
                    App.Telemetry.Context.Properties["connection.detail"] = string.Format("{0:X}", string.Format("{0}:{1}", host, port).GetHashCode());
                    App.Connection = new NetworkSerial( new Windows.Networking.HostName( host ), portnum );
                    break;
            }

            App.Telemetry.Context.Properties["connection.type"] = App.Connection.GetType().Name;
            App.Telemetry.Context.Properties["connection.state"] = "Connecting";
            App.Telemetry.TrackEvent("Connection_Attempt");

            App.Arduino = new RemoteDevice( App.Connection );
            App.Arduino.DeviceReady += OnDeviceReady;
            App.Arduino.DeviceConnectionFailed += OnConnectionFailed;

            connectionStopwatch.Reset();
            connectionStopwatch.Start();

            App.Connection.begin( baudRate, SerialConfig.SERIAL_8N1 );

            //start a timer for connection timeout
            timeout = new DispatcherTimer();
            timeout.Interval = new TimeSpan( 0, 0, 30 );
            timeout.Tick += Connection_TimeOut;
            timeout.Start();
        }


        /****************************************************************
         *                  Event callbacks                             *
         ****************************************************************/

        private void OnConnectionFailed( string message )
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                timeout.Stop();
                ConnectMessage.Text = "Connection attempt failed: " + message;

                //telemetry
                connectionStopwatch.Stop();
                App.Telemetry.Context.Properties["connection.state"] = "Failed";
                TrackConnectionEvent(ConnectMessage.Text, connectionStopwatch);

                Reset();
            } ) );
        }

        private void OnDeviceReady()
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                timeout.Stop();
                ConnectMessage.Text = "Successfully connected!";

                //telemetry
                connectionStopwatch.Stop();
                App.Telemetry.Context.Properties["connection.state"] = "Connected";
                TrackConnectionEvent(ConnectMessage.Text, connectionStopwatch);
                App.Telemetry.TrackMetric( "Connection_Page_Time_Spent_In_Seconds", ( DateTime.Now - timePageNavigatedTo ).TotalSeconds );

                this.Frame.Navigate( typeof( MainPage ) );
            } ) );
        }

        private void Connection_TimeOut( object sender, object e )
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                timeout.Stop();
                ConnectMessage.Text = "Connection attempt timed out.";

                //telemetry
                App.Telemetry.Context.Properties["connection.state"] = "Timeout";
                connectionStopwatch.Stop();
                TrackConnectionEvent(ConnectMessage.Text, connectionStopwatch);
                
                Reset();
            } ) );
        }

        /// <summary>
        /// This function is invoked if a cancellation is invoked for any reason on the connection task
        /// </summary>
        private void OnConnectionCancelled()
        {
            timeout.Stop();
            ConnectMessage.Text = "Connection attempt cancelled.";

            //telemetry
            App.Telemetry.Context.Properties["connection.state"] = "Cancelled";
            connectionStopwatch.Stop();
            TrackConnectionEvent(ConnectMessage.Text, connectionStopwatch);

            Reset();
        }

        /****************************************************************
         *                  Helper functions                            *
         ****************************************************************/

        private void SetUiEnabled( bool enabled )
        {
            RefreshButton.IsEnabled = enabled;
            ConnectButton.IsEnabled = enabled;
            CancelButton.IsEnabled = !enabled;
        }

        private void Reset()
        {
            if( App.Connection != null )
            {
                App.Connection.ConnectionEstablished -= OnDeviceReady;
                App.Connection.ConnectionFailed -= OnConnectionFailed;
                App.Connection.end();
            }

            if( cancelTokenSource != null )
            {
                cancelTokenSource.Dispose();
            }

            App.Connection = null;
            App.Arduino = null;
            cancelTokenSource = null;

            SetUiEnabled( true );
        }

        private void TrackConnectionEvent(string message, Stopwatch stopwatch)
        {
            var metrics = new Dictionary<string, double>
                {
                    {"connection.elapsed", stopwatch.Elapsed.TotalMilliseconds}
                };

            var telemetryProperties = new Dictionary<string, string>
                {
                    {"connection.message", message}
                };

            App.Telemetry.TrackEvent("Connection", telemetryProperties, metrics);
        }


        /****************************************************************
         *                       Menu Bar Callbacks                     *
         ****************************************************************/
        /// <summary>
        /// Called if the pointer hovers over the Digital button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void DigitalButton_Enter(object sender, RoutedEventArgs e)
        {
            DigitalRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Called if the pointer hovers over the Analog button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void AnalogButton_Enter(object sender, RoutedEventArgs e)
        {
            AnalogRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Called if the pointer hovers over the PWM button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void PWMButton_Enter(object sender, RoutedEventArgs e)
        {
            PWMRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Called if the pointer hovers over the Servo button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void ServoButton_Enter(object sender, RoutedEventArgs e)
        {
            ServoRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Called if the pointer hovers over the About button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void AboutButton_Enter(object sender, RoutedEventArgs e)
        {
            AboutRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Called if the pointer exits the boundaries of any button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void Button_Exit(object sender, RoutedEventArgs e)
        {
            DigitalRectangle.Visibility = Visibility.Collapsed;
            AnalogRectangle.Visibility = Visibility.Collapsed;
            PWMRectangle.Visibility = Visibility.Collapsed;
            AboutRectangle.Visibility = Visibility.Collapsed;
        }
    }
}
