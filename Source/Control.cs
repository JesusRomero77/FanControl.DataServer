namespace FanControl.DataServer
{
    /// <summary>
    /// Control de FanControl: además del Identifier y el Name que hereda, guarda la curva que
    /// tiene asignada (DataSource) y su última lectura (Value).
    /// </summary>
    public class Control : FanControlElement
    {
        // Nombre de la curva asignada al control (SelectedFanCurve.Name en el JSON de FanControl).
        public string DataSource { get; }

        // Última lectura (PWM) con decimales; null si el RPC no la incluyó.
        public double? Value { get; set; }

        public Control(string identifier, string name, string dataSource) : base(identifier, name)
        {
            DataSource = dataSource;
        }
    }
}