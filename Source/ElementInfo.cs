namespace FanControl.DataServer
{
    // Un sensor o control del perfil, tal como se describe en /profile.
    public class ElementInfo
    {
        public string Identifier { get; set; }
        public string Name { get; set; }

        // Sensores: Identifier del sensor físico. Controles: nombre de la curva asignada.
        public string DataSource { get; set; }
    }
}