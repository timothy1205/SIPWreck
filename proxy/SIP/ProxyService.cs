
using System.Net;
using SIPSorcery.SIP;

namespace SIPWreck.SIP
{
    class ProxyService(ILogger<ProxyService> logger, RegistrarService registrar) : IHostedService
    {
        private SIPTransport? _sipTransport;
        private SIPWebSocketChannel? _webSocketChannel;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var address = IPAddress.Any;
            var port = 5066;
            logger.LogInformation("Starting on {Address}:{Port}", address, port);

            _sipTransport = new SIPTransport();
            _webSocketChannel = new SIPWebSocketChannel(address, port);
            _sipTransport.AddSIPChannel(_webSocketChannel);

            _sipTransport.SIPTransportRequestReceived += SIPRequestReceived;
            _sipTransport.SIPTransportResponseReceived += SIPResponseReceived;

            
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping...");

            _sipTransport?.Shutdown();
            _webSocketChannel?.Dispose();

            return Task.CompletedTask;
        }

        private async Task SendResponse(SIPResponse sipResponse)
        {
            if (_sipTransport is null) throw new InvalidOperationException("Expected non-null transport");

            logger.LogTrace("Sending response message:\n{Response}", sipResponse);
            await _sipTransport.SendResponseAsync(sipResponse);
        }


        // Delegates
        private async Task SIPRequestReceived(SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest) 
        {
            logger.LogTrace("Received request message from {Remote}\n{Request}", remoteEndPoint, sipRequest);
            SIPResponse response;

            switch (sipRequest.Method)
            {
                case SIPMethodsEnum.OPTIONS:
                    response = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                    await SendResponse(response);
                    break;
                case SIPMethodsEnum.REGISTER:
                    if (registrar.TryRegister(sipRequest, localEndPoint, remoteEndPoint, out string? rejectMsg))
                    {
                        response = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                        await SendResponse(response);
                    }
                    else {
                        response = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Decline, rejectMsg);
                        await SendResponse(response);
                    }
                    break;
                default:
                    break;
            }
        }

        private Task SIPResponseReceived(SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPResponse sipResponse) 
        {
            logger.LogTrace("Received response message from {Remote}\n{Reponse}", remoteEndPoint, sipResponse);

            return Task.CompletedTask;
        }
    }
}
