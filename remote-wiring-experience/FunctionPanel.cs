using System;
using System.Collections.Generic;
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
                    functionElement.ResetOperandComboBoxes( numberOfBytes );
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

            AddFunctionElement( true );
        }

        public double ProcessI2cReply( byte[] rawdata )
        {
            double val = 0;

            //each child of this panel, except for the last, is a FunctionElement which can perform a function.
            for( int i = 0; i < Children.Count - 1; ++i )
            {
                var functionElement = Children[i] as FunctionElement;
                val = functionElement.Process( val, rawdata );
            }
            return val;
        }

        /// <summary>
        /// Adds a function element to the UI for programming
        /// </summary>
        public void AddFunctionElement( bool isFirstElement )
        {
            Children.Insert( Children.Count - 1, CreateFunctionElement( isFirstElement ) );
        }

        private void OnClick_AddFunctionButton( object sender, RoutedEventArgs e )
        {
            AddFunctionElement( false );
        }

        /// <summary>
        /// Creates a function element which can be set to perform one operation of a (optional) series
        /// </summary>
        /// <returns></returns>
        private UIElement CreateFunctionElement( bool isFirstElement )
        {
            var element = new FunctionElement( isFirstElement );
            element.ResetOperandComboBoxes( numberOfBytes );
            return element;
        }
    }
}
