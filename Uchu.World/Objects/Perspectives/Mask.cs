namespace Uchu.World
{
    public readonly struct Mask
    {
        public readonly long Value;

        public Mask(long value)
        {
            Value = value;
        }

        public static Mask operator |(Mask a, Mask b)
        {
            return a.Value | b.Value;
        }
        
        public static Mask operator &(Mask a, Mask b)
        {
            return a.Value & b.Value;
        }
        
        public static Mask operator ~(Mask a)
        {
            return ~a.Value;
        }
        
        public static Mask operator +(Mask a, Mask b)
        {
            return a.Value | b.Value;
        }
        
        public static Mask operator -(Mask a, Mask b)
        {
            return a.Value &~ b.Value;
        }
        
        public static bool operator ==(Mask a, Mask b)
        {
            return (a.Value & b.Value) != 0;
        }

        public static bool operator !=(Mask a, Mask b)
        {
            return (a.Value & b.Value) == 0;
        }

        public static implicit operator long(Mask mask) => mask.Value;
        
        public static implicit operator Mask(long value) => new Mask(value);
        
        public bool Equals(Mask other)
        {
            return Value == other.Value;
        }
        
        public bool Equals(long value)
        {
            return Value == value;
        }

        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case Mask m: return m.Value == Value;
                case long l: return l == Value;
                default: return false;
            }
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}