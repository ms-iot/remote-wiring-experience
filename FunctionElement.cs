using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace remote_wiring_experience
{

    class FunctionElement : StackPanel
    {
        private bool isFirstElement;
        private ComboBox functionComboBox;
        private ComboBox operand1ComboBox;
        private ComboBox operand2ComboBox;
        private TextBox valueTextBox;

        /// <summary>
        /// This constructor requires no arguments, which will just build a raw interface
        /// </summary>
        /// <param name="numberOfBytes"></param>
        public FunctionElement( bool isFirstElement )
        {
            this.isFirstElement = isFirstElement;
            BuildInterface();
        }

        /// <summary>
        /// This function allows the target combo box to be reconfigured without resetting the entire UI
        /// </summary>
        internal void ResetOperandComboBoxes( uint numberOfBytes )
        {
            operand2ComboBox.Items.Clear();

            //handle the 1st operand, which is different if this is the first FunctionElement
            if( isFirstElement )
            {
                operand1ComboBox.IsEnabled = true;
                operand1ComboBox.Items.Clear();
                operand1ComboBox.Items.Add( "Value: 0" );
                for( int i = 0; i < numberOfBytes; ++i )
                {
                    operand1ComboBox.Items.Add( "Byte index " + i );
                }
            }

            operand2ComboBox.Items.Add( "Value" );
            for( int i = 0; i < numberOfBytes; ++i )
            {
                operand2ComboBox.Items.Add( "Byte index " + i );
            }
        }

        /// <summary>
        /// This function accepts the current running calculation (input) and the raw data and performs the user-requested
        /// function that this element has been set up to perform.
        /// <para>Throws FormatException if the entered text is not a number.</para>
        /// <para>Throws IndexOutOfRangeException if the selected byte index is greater or equal to the length of rawdata</para>
        /// </summary>
        /// <param name="input">The current running calculation value</param>
        /// <param name="rawdata">The raw reply data from the device</param>
        /// <returns></returns>
        internal double Process( double input, byte[] rawdata )
        {
            if( isFirstElement && operand1ComboBox.SelectedIndex == -1 ) return input;
            if( operand2ComboBox.SelectedIndex == -1 || functionComboBox.SelectedIndex == -1 ) return input;

            double operand1 = input;
            if( isFirstElement )
            {
                var str1 = operand1ComboBox.SelectedItem as String;
                if( str1.Contains( "Byte index " ) )
                {
                    var indexLocation = "Byte index ".Length;
                    var index = Convert.ToInt32( str1.Substring( indexLocation ) );
                    operand1 = rawdata[index];
                }
            }

            double operand2;
            var str = operand2ComboBox.SelectedItem as String;

            if( str.Equals( "Value" ) )
            {
                string text = valueTextBox.Text;

                //did they enter a number in binary or hex format?
                if( text.Contains( "x" ) )
                {
                    operand2 = Convert.ToInt32( text.Substring( text.IndexOf( "x" ) + 1 ), 16 );
                }
                else if( text.Contains( "b" ) )
                {
                    operand2 = Convert.ToInt32( text.Substring( text.IndexOf( 'b' ) + 1 ), 2 );
                }
                else
                {
                    operand2 = Convert.ToInt32( text );
                }
            }
            else if( str.Contains( "Byte index " ) )
            {
                var indexLocation = "Byte index ".Length;
                var index = Convert.ToInt32( str.Substring( indexLocation ) );
                operand2 = rawdata[index];
            }
            else
            {
                //this should never happen as the targetComboBox can only be "Value" or "Byte Index ?"
                throw new Exception( "An unexpected failure occurred in FunctionElement.Process()" );
            }

            var function = functionComboBox.SelectedItem as Function;
            return function.Process( operand1, operand2 );
        }

        /// <summary>
        /// This function sets up the UI which will display the options for computing one step of an end result from a raw reply.
        /// </summary>
        private void BuildInterface()
        {
            Orientation = Orientation.Vertical;

            var text = new TextBlock();
            text.Text = isFirstElement ? "How to process my reply:" : "Then:";
            Children.Add( text );

            //Create the entire horizontal layout which will contain all of the drop downs and textboxes
            var functionstack = new StackPanel();
            functionstack.Orientation = Orientation.Horizontal;
            functionstack.FlowDirection = FlowDirection.LeftToRight;
            functionstack.Margin = new Thickness( 5 );
            functionstack.BorderBrush = new SolidColorBrush( Color.FromArgb( 255, 0, 0, 0 ) );
            functionstack.BorderThickness = new Thickness( 2 );

            //Create a vertical stack which will contain a drop down for the first operand and text description
            var itemstack = new StackPanel();
            itemstack.Margin = new Thickness( 5 );
            itemstack.Orientation = Orientation.Vertical;
            text = new TextBlock();
            text.Text = "1st Operand:";
            itemstack.Children.Add( text );

            operand1ComboBox = new ComboBox();
            operand1ComboBox.Items.Add( "Previous Answer" );
            operand1ComboBox.SelectedIndex = 0;
            operand1ComboBox.IsEnabled = false;
            itemstack.Children.Add( operand1ComboBox );
            functionstack.Children.Add( itemstack );
            
            //Create a vertical stack which will contain a drop down for the function and text description
            itemstack = new StackPanel();
            itemstack.Margin = new Thickness( 5 );
            itemstack.Orientation = Orientation.Vertical;
            text = new TextBlock();
            text.Text = "Function:";
            itemstack.Children.Add( text );

            functionComboBox = new ComboBox();
            foreach( FunctionType function in Enum.GetValues( typeof( FunctionType ) ) )
            {
                functionComboBox.Items.Add( new Function( function ) );
            }
            itemstack.Children.Add( functionComboBox );

            functionstack.Children.Add( itemstack );

            //Create a vertical stack which will contain a drop down for the 2nd operand and text description
            itemstack = new StackPanel();
            itemstack.Margin = new Thickness( 5 );

            text = new TextBlock();
            text.Text = "2nd Operand:";
            itemstack.Children.Add( text );

            operand2ComboBox = new ComboBox();
            operand2ComboBox.SelectionChanged += OnSelectionChanged_Operand2ComboBox;
            itemstack.Children.Add( operand2ComboBox );

            functionstack.Children.Add( itemstack );

            //finally, a textbox for a manually-entered value which will only appear if they select "value" as the 2nd operand
            valueTextBox = new TextBox();
            valueTextBox.Visibility = Visibility.Collapsed;
            valueTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            valueTextBox.VerticalAlignment = VerticalAlignment.Center;
            valueTextBox.Margin = new Thickness( 5, 12, 5, 0 );
            functionstack.Children.Add( valueTextBox );

            Children.Add( functionstack );
            
            ResetOperandComboBoxes( 0 );
        }

        private void OnSelectionChanged_Operand2ComboBox( object sender, SelectionChangedEventArgs e )
        {
            var combo = sender as ComboBox;
            if( combo.SelectedItem.Equals( "Value" ) )
            {
                valueTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                valueTextBox.Visibility = Visibility.Collapsed;
            }
        }
    }
}
