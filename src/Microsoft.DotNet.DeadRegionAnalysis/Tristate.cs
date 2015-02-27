// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    public struct Tristate
    {
        private byte _value;

        public static Tristate False = new Tristate(0);
        public static readonly Tristate True = new Tristate(1);
        public static readonly Tristate Varying = new Tristate(2);

        private Tristate(byte value)
        {
            _value = value;
        }

        public static Tristate operator !(Tristate state)
        {
            if (state == Varying)
            {
                return state;
            }
            else
            {
                return new Tristate((byte)(state._value ^ 1));
            }
        }

        public static bool operator ==(Tristate x, Tristate y)
        {
            return x._value == y._value;
        }

        public static bool operator !=(Tristate x, Tristate y)
        {
            return x._value != y._value;
        }

        public static Tristate operator &(Tristate x, Tristate y)
        {
            if (x == False || y == False)
            {
                // false && anything == false
                return False;
            }
            if (x == True && y == True)
            {
                // true && true == true
                return True;
            }
            // true && varying == varying
            return Varying;
        }

        public static Tristate operator |(Tristate x, Tristate y)
        {
            if (x == True || y == True)
            {
                // true || anything == true
                return True;
            }
            if (x == False && y == False)
            {
                // false && false == false
                return False;
            }
            // false || varying == varying
            return Varying;
        }

        public override bool Equals(object obj)
        {
            var state = obj as Tristate?;
            if (state == null)
            {
                return false;
            }

            return this == state.Value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            switch (_value)
            {
                case 0:
                    return "false";
                case 1:
                    return "true";
                case 2:
                    return "varying";
                default:
                    Debug.Assert(false);
                    return string.Empty;
            }
        }

        public static Tristate Parse(string s)
        {
            if (s == null)
            {
                throw new ArgumentNullException();
            }

            if (s.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return False;
            }
            else if (s.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return True;
            }
            else if (s.Equals("varying", StringComparison.OrdinalIgnoreCase))
            {
                return Varying;
            }
            else
            {
                throw new FormatException(string.Format("Input string '{0}' was not in a correct format", s));
            }
        }
    }
}
