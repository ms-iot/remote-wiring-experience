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
        private uint numberOfBytes;

        /// <summary>
        /// Allows the number of bytes expected in an I2C reply to be changed without drastically re-configuring the UI
        /// </summary>
        public uint NumberOfBytes
        {
            get
            {
                return numberOfBytes;
            }
            set
            {
                numberOfBytes = value;
                for( int i = 0; i < Children.Count - 1; ++i )
                {
                    var functionElement = Children[i] as FunctionElement;
                    functionElement.ResetTargetComboBox( numberOfBytes );
                }
            }
        }

        public FunctionPanel( uint numberOfBytes )
        {
            this.numberOfBytes = numberOfBytes;
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

        /// <summary>
        /// Adds a function element to the UI for programming
        /// </summary>
        public void AddFunctionElement()
        {
            Children.Insert( Children.Count - 1, CreateFunctionElement() );
        }

        /// <summary>
        /// Creates a function element which can be set to perform one operation of a (optional) series
        /// </summary>
        /// <returns></returns>
        private UIElement CreateFunctionElement()
        {
            var element = new FunctionElement();
            element.ResetTargetComboBox( numberOfBytes );
            return element;
        }
    }
}
