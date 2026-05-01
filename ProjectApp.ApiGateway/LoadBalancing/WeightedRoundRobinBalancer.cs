using Ocelot.LoadBalancer.Errors;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.Values;

namespace ProjectApp.ApiGateway.LoadBalancing;

/// <summary>
/// Weighted Round Robin балансировщик нагрузки для Ocelot.
/// </summary>
public class WeightedRoundRobinBalancer(Func<Task<List<Service>>> servicesProvider, Dictionary<string, int> hostPortWeights) : ILoadBalancer
{
    private int _currentIndex = -1;
    private int _remainingRequests = 0;
    private readonly object _lock = new();

    public string Type => nameof(WeightedRoundRobinBalancer);

    public async Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext httpContext)
    {
        var services = await servicesProvider();

        if (services == null || services.Count == 0)
        {
            return new ErrorResponse<ServiceHostAndPort>(
                new ServicesAreEmptyError("No services available"));
        }

        var availableServices = services
            .Where(s =>
            {
                var hostPort = $"{s.HostAndPort.DownstreamHost}:{s.HostAndPort.DownstreamPort}";
                var weight = hostPortWeights.GetValueOrDefault(hostPort, 1);
                return weight > 0;
            })
            .ToList();

        if (availableServices.Count == 0)
        {
            return new ErrorResponse<ServiceHostAndPort>(
                new ServicesAreEmptyError("No services with positive weight available"));
        }

        ServiceHostAndPort selectedService;

        lock (_lock)
        {
            if (_remainingRequests <= 0)
            {
                _currentIndex = (_currentIndex + 1) % availableServices.Count;

                var service = availableServices[_currentIndex];
                var hostPort = $"{service.HostAndPort.DownstreamHost}:{service.HostAndPort.DownstreamPort}";

                var weight = hostPortWeights.GetValueOrDefault(hostPort, 1);
                _remainingRequests = weight;
            }

            var currentService = availableServices[_currentIndex];
            selectedService = currentService.HostAndPort;

            _remainingRequests--;
        }
        return new OkResponse<ServiceHostAndPort>(selectedService);
    }

    public void Release(ServiceHostAndPort hostAndPort) {}
}
