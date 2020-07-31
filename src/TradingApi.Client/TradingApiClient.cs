using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Lykke.HftApi.ApiContract;

namespace TradingApi.Client
{
    public class TradingApiClient
    {
        protected GrpcChannel Channel { get; }

        public PublicService.PublicServiceClient PublicApi { get; private set; }

        public PrivateService.PrivateServiceClient PrivateApi { get; private set; }

        public TradingApiClient(string grpcUrl, string apiKey)
        {
            var credentials = CallCredentials.FromInterceptor((context, metadata) =>
            {
                if (!string.IsNullOrEmpty(apiKey))
                {
                    metadata.Add("Authorization", $"Bearer {apiKey}");
                }
                return Task.CompletedTask;
            });

            Channel = GrpcChannel.ForAddress(grpcUrl, new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.Create(new SslCredentials(), credentials)
            });

            PublicApi = new PublicService.PublicServiceClient(Channel);

            PrivateApi = new PrivateService.PrivateServiceClient(Channel);
        }
    }
}
