namespace FanControl.DataServer
{
    // Respuesta de /challenge: el número de un solo uso que el cliente debe firmar.
    public class ChallengeInfo
    {
        public string Nonce { get; set; }
    }
}