using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.Maker.Serial;
using Microsoft.Maker.RemoteWiring;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace remote_wiring_experience
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DispatcherTimer timeout;

        public MainPage()
        {
            this.InitializeComponent();
            ConnectionMethodComboBox.SelectionChanged += ConnectionComboBox_SelectionChanged;
        }

        protected override void OnNavigatedTo( NavigationEventArgs e )
        {
            base.OnNavigatedTo( e );
            if( ConnectionList.ItemsSource == null )
            {
                ConnectMessage.Text = "Select an item to connect to.";
                RefreshDeviceList();
            }
        }

        private void RefreshDeviceList()
        {
            //invoke the listAvailableDevicesAsync method of the correct Serial class. Since it is Async, we will wrap it in a Task and add a llambda to execute when finished
            Task<DeviceInformationCollection> task = null;
            switch( ConnectionMethodComboBox.SelectedIndex )
            {
                //bluetooth
                default:
                case 0:
                    task = BluetoothSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>();
                    break;

                //usb
                case 1:
                    task = UsbSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>();
                    break;
            }

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

        /****************************************************************
         *                       UI Callbacks                           *
         ****************************************************************/

        private void ConnectionComboBox_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            RefreshDeviceList();
        }

        private void RefreshButton_Click( object sender, RoutedEventArgs e )
        {
            RefreshDeviceList();
        }

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
            else if( ConnectionMethodComboBox.SelectedIndex != 2 )
            {
                //if they haven't selected an item, but have chosen "usb" or "bluetooth", we can't proceed
                ConnectMessage.Text = "You must select an item to proceed.";
                SetUiEnabled( true );
                return;
            }

            //use the selected device to create our communication object
            switch( ConnectionMethodComboBox.SelectedItem as String )
            {
                default:
                case "Bluetooth":
                    App.Connection = new BluetoothSerial( device );
                    break;
                    
                case "USB":
                    App.Connection = new UsbSerial( device );
                    break;
                    
                case "Network":
                    App.Connection = new NetworkSerial( new Windows.Networking.HostName( "192.168.1.120" ), 5000 );
                    break;
            }

            App.Arduino = new RemoteDevice( App.Connection );
            App.Connection.begin( 115200, SerialConfig.SERIAL_8N1 );
            App.Connection.ConnectionEstablished += OnConnectionEstablished;
            App.Connection.ConnectionFailed += OnConnectionFailed;

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
            timeout.Stop();
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                ConnectMessage.Text = "Connection attempt failed: " + message;
                SetUiEnabled( true );
            } ) );
        }

        private void OnConnectionEstablished()
        {
            timeout.Stop();
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                this.Frame.Navigate( typeof( GpioPage ) );
            } ) );
        }

        private void Connection_TimeOut( object sender, object e )
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                ConnectMessage.Text = "Connection attempt timed out.";
                SetUiEnabled( true );
            } ) );
        }


        /****************************************************************
         *                  Helper functions                            *
         ****************************************************************/

        private void SetUiEnabled( bool enabled )
        {
            RefreshButton.IsEnabled = enabled;
            ConnectButton.IsEnabled = enabled;
        }
    }
}
