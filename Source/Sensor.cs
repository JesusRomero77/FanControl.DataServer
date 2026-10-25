namespace FanControl.DataServer
{
    /// <summary>
    /// Sensor de FanControl: además del Identifier y el Name que hereda, guarda el sensor físico
    /// del que toma su temperatura (DataSource) y su última lectura (Value).
    /// </summary>
    public class Sensor : FanControlElement
    {
        // Identifier del sensor físico del que este sensor toma su valor
        // (SelectedTempSource.Identifier en el JSON de FanControl).
        public string DataSource { get; }

        // Última lectura con decimales; null si el RPC no la incluyó.
        public double? Value { get; set; }

        public Sensor(string identifier, string name, string dataSource) : base(identifier, name)
        {
            DataSource = dataSource;
        }
    }
}