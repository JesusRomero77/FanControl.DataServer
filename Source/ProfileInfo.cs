using System.Collections.Generic;

namespace FanControl.DataServer
{
    // Respuesta de /profile: el perfil activo y los elementos que contiene.
    public class ProfileInfo
    {
        // Sube en uno cada vez que se prepara un perfil (también si se vuelve a cargar el mismo).
        public int ProfileId { get; set; }
        public string ProfileName { get; set; }
        public List<ElementInfo> Sensors { get; set; }
        public List<ElementInfo> Controls { get; set; }
    }
}