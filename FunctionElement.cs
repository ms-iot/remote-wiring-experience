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
        private ComboBox functionComboBox;
        private ComboBox targetComboBox;

        /// <summary>
        /// This constructor requires the number of bytes that are to be expected in an I2C reply.
        /// It will use this information to set up the 'targets' drop down
        /// </summary>
        /// <param name="numberOfBytes"></param>
        public FunctionElement()
        {
            BuildInterface();
        }

        /// <summary>
        /// This function allows the target combo box to be reconfigured without resetting the entire UI
        /// </summary>
        public void ResetTargetComboBox( uint numberOfBytes )
        {
            targetComboBox.Items.Clear();
            targetComboBox.Items.Add( "Value" );
            for( int i = 0; i < numberOfBytes; ++i )
            {
                targetComboBox.Items.Add( "Byte index " + i );
            }
        }

        /// <summary>
        /// This function sets up the UI which will display the options for computing one step of an end result from a raw reply.
        /// </summary>
        private void BuildInterface()
        {
            Orientation = Orientation.Vertical;

            var text = new TextBlock();
            text.Text = "Then:";
            Children.Add( text );

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

            functionComboBox = new ComboBox();
            foreach( FunctionType function in Enum.GetValues( typeof( FunctionType ) ) )
            {
                functionComboBox.Items.Add( new Function( function ) );
            }
            itemstack.Children.Add( functionComboBox );

            functionstack.Children.Add( itemstack );

            itemstack = new StackPanel();
            itemstack.Margin = new Thickness( 5 );

            text = new TextBlock();
            text.Text = "Target:";
            itemstack.Children.Add( text );

            targetComboBox = new ComboBox();
            ResetTargetComboBox( 0 );
            itemstack.Children.Add( targetComboBox );

            functionstack.Children.Add( itemstack );

            var box = new TextBox();
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            box.VerticalAlignment = VerticalAlignment.Center;
            box.Margin = new Thickness( 5, 12, 5, 0 );
            functionstack.Children.Add( box );

            Children.Add( functionstack );
        }
    }
}
