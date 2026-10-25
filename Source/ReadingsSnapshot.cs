using System.Collections.Generic;

namespace FanControl.DataServer
{
    // Respuesta de /readings: una foto de todas las lecturas en un instante.
    public class ReadingsSnapshot
    {
        // Número que sube en cada foto; si deja de subir, los datos están parados.
        public long Sequence { get; set; }

        // Hora UTC de captura, con formato dd-MM-yy HH:mm:ss.
        public string TimestampUtc { get; set; }

        // Perfil al que pertenecen las lecturas; si no coincide con el de /profile, hay que volver a pedir /profile.
        public int ProfileId { get; set; }
        public List<Reading> Sensors { get; set; }
        public List<Reading> Controls { get; set; }
    }
}