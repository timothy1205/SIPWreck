
using SIPSorcery.SIP;
using SIPWreck.Models.Config;
using SIPWreck.Models.SIP;

namespace SIPWreck.SIP
{
    public class RegistrarService(
        ILogger<RegistrarService> logger,
        IConfiguration config
    ) : IHostedService, IDisposable
    {
        private class DateTimeComparer : IComparer<DateTime>
        {
            public int Compare(DateTime x, DateTime y)
            {
                return (int)(x - y).TotalMilliseconds;
            }
        }

        private readonly ILogger<RegistrarService> _logger = logger;
        private readonly RegistrarConfig _registrarConfig = config.GetRequiredSection("Registrar")?.Get<RegistrarConfig>() ?? throw new InvalidOperationException("Expected Registar Config");
        private PriorityQueue<Registration, DateTime> _expiration = new(new DateTimeComparer());
        private Dictionary<SIPURI, Registration> _registrations = new();
        private Timer? _timer;
        private bool _checkingExpired = false;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting timer with a period of {TimerPeriod} seconds", _registrarConfig.TimerPeriod);

            _timer = new Timer(CheckExpiredRegistrations, null, TimeSpan.Zero, TimeSpan.FromSeconds(_registrarConfig.TimerPeriod));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Stopping timer");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public bool TryRegister(SIPRequest sipRequest, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, out string? rejectMsg)
        {
            _logger.LogDebug("Attempting to register {FromURI}", sipRequest.Header.From.FromURI);

            var uri = sipRequest.Header.From.FromURI;
            var expires = sipRequest.Header.Expires;

            if (expires < 0)
            {
                rejectMsg = "Expires Required";
                return false;
            }
            else if (expires == 0)
            {
                if (!_registrations.ContainsKey(uri))
                {
                    _logger.LogInformation("Ignoring unknown unregister from {URI}", uri);
                    rejectMsg = null;
                    return true;
                }

                if (_registrations.Remove(uri, out var removed))
                {
                    _expiration.Remove(removed, out _, out _);
                }

                _logger.LogInformation("{URI} unregistered", uri);
            }
            else if (expires > _registrarConfig.MaxExpiration)
            {
                rejectMsg = $"Expires Limit Exceeded ({_registrarConfig.MaxExpiration})";
                return false;
            }

            if (!Registration.TryParse(sipRequest, localEndPoint, remoteEndPoint, out var registration))
            {
                rejectMsg = "Invalid";
                return false;
            }

            _expiration.Enqueue(registration, registration.Expiration);
            _registrations[registration.URI] = registration;

            _logger.LogInformation("{URI} registered for {Expires} seconds", uri, expires);

            rejectMsg = null;
            return true;
        }

        private void CheckExpiredRegistrations(object? state)
        {
            if (_checkingExpired) return;

            _checkingExpired = true;
            _logger.LogTrace("Checking for inactive registrations");

            while (ExpiredRegistrationExists())
            {
                var removed = _expiration.Dequeue();
                _registrations.Remove(removed.URI);
                _logger.LogInformation("{URI}'s registration has expired and was removed", removed.URI);
            }

            _checkingExpired = false;
        }

        private bool ExpiredRegistrationExists()
        {
            return _expiration.TryPeek(out var _, out var priority) && priority < DateTime.Now;
        }
    }
}