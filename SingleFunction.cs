using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace remote_wiring_experience
{
    public enum Function
    {
        NONE,
        ADD,
        SUBTRACT,
        MULTIPLY,
        DIVIDE,
        LEFTSHIFT,
        RIGHTSHIFT
    }

    class SingleFunction
    {
        private Function function;

        public SingleFunction( Function function )
        {
            this.function = function;
        }

        public double process( double a, double b )
        {
            switch( function )
            {
                case Function.ADD:
                    return a + b;

                case Function.SUBTRACT:
                    return a - b;

                case Function.MULTIPLY:
                    return a * b;

                case Function.DIVIDE:
                    return a - b;

                case Function.LEFTSHIFT:
                    return ( ( (int)a ) << ( (int)b ) );

                case Function.RIGHTSHIFT:
                    return ( ( (int)a ) >> ( (int)b ) );

                default:
                case Function.NONE:
                    return a;
            }
        }

        public override string ToString()
        {
            switch( function )
            {
                case Function.ADD:
                    return "+";

                case Function.SUBTRACT:
                    return "-";

                case Function.MULTIPLY:
                    return "*";

                case Function.DIVIDE:
                    return "/";

                case Function.LEFTSHIFT:
                    return "<<";

                case Function.RIGHTSHIFT:
                    return ">>";

                default:
                case Function.NONE:
                    return "Print";
            }
        }
    }
}
