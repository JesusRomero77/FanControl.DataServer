namespace FanControl.DataServer
{
    /// <summary>
    /// Clase base de los elementos de FanControl que el plugin sirve (sensores y controles).
    /// Guarda lo que tienen en común: el Identifier que los distingue y el Name con el que
    /// se muestran. No se usa directamente; la heredan Sensor y Control.
    /// </summary>
    public abstract class FanControlElement
    {
        // Identificador único del elemento en FanControl.
        public string Identifier { get; }

        // Nombre con el que se muestra el elemento (NickName en el JSON de controles y sensores).
        public string Name { get; }

        protected FanControlElement(string identifier, string name)
        {
            Identifier = identifier;
            Name = name;
        }
    }
}