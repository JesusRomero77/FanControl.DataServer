namespace FanControl.DataServer
{
    // Una lectura de un sensor o control; Value es null si el RPC no la incluyó.
    public class Reading
    {
        public string Identifier { get; set; }
        public double? Value { get; set; }
    }
}