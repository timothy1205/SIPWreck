
using SIPSorcery.SIP;

namespace SIPWreck.Models.SIP
{
    public class Registration
    {
        public required SIPURI URI { get; set; }
        public required DateTime Expiration { get; set; }
        public required SIPEndPoint LocalEndPoint { get; set; }
        public required SIPEndPoint RemoteEndPoint { get; set; }

        public static bool TryParse(SIPRequest sipRequest, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, out Registration registration)
        {
            var parsedRegistration = new Registration()
            {
                URI = sipRequest.Header.From.FromURI.CopyOf(),
                Expiration = DateTime.Now.AddSeconds(sipRequest.Header.Expires),
                LocalEndPoint = localEndPoint,
                RemoteEndPoint = remoteEndPoint,
            };

            registration = parsedRegistration;  
            return true;
        }
    }
}