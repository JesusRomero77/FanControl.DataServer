using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

namespace FanControl.DataServer
{
    // Guarda la última foto del perfil y de las lecturas. Cada foto es un objeto nuevo que no se
    // modifica después de publicarse, así el servidor HTTP puede leerla desde otro hilo sin bloqueos.
    internal static class SnapshotStore
    {
        private static volatile ProfileInfo _profile;
        private static volatile ReadingsSnapshot _readings;
        private static int _profileId;
        private static long _sequence;

        public static ProfileInfo Profile => _profile;
        public static ReadingsSnapshot Readings => _readings;

        // Publica el perfil activo con sus sensores y controles; lo llama InitialStartup.
        public static void PublishProfile(string fileName, List<Sensor> sensors, List<Control> controls)
        {
            _profile = new ProfileInfo
            {
                ProfileId = Interlocked.Increment(ref _profileId),
                ProfileName = Path.GetFileNameWithoutExtension(fileName),
                Sensors = sensors.Select(sensor => new ElementInfo { Identifier = sensor.Identifier, Name = sensor.Name, DataSource = sensor.DataSource }).ToList(),
                Controls = controls.Select(control => new ElementInfo { Identifier = control.Identifier, Name = control.Name, DataSource = control.DataSource }).ToList()
            };
        }

        // Publica una foto nueva de las lecturas; la llama Update tras leer el RPC.
        public static void PublishReadings(List<Sensor> sensors, List<Control> controls)
        {
            _readings = new ReadingsSnapshot
            {
                Sequence = Interlocked.Increment(ref _sequence),
                TimestampUtc = DateTime.UtcNow.ToString("dd-MM-yy HH:mm:ss", CultureInfo.InvariantCulture),
                ProfileId = Volatile.Read(ref _profileId),
                Sensors = sensors.Select(sensor => new Reading { Identifier = sensor.Identifier, Value = sensor.Value }).ToList(),
                Controls = controls.Select(control => new Reading { Identifier = control.Identifier, Value = control.Value }).ToList()
            };
        }
    }
}