# Example of usage Lykke Trading API

Lykke Trading API documentation: [https://lykkecity.github.io/Trading-API/](https://lykkecity.github.io/Trading-API/)

## Client library

To build client librari in dotnet core 3 need to add .proto files into project and configurate autogeneration for gRPC.

Details you can see here: [TradingApi.Client](https://github.com/LykkeCity/Lykke-TradingAPI-Examples/tree/master/src/TradingApi.Client)

In class [TradingApiClient](https://github.com/LykkeCity/Lykke-TradingAPI-Examples/blob/master/src/TradingApi.Client/TradingApiClient.cs#L28) you can see how to create gRPC channel with api key. And create a PublicApi client and a PrivateApi client.


## Example: How to follow prices

source code:[Example.FollowPrices](https://github.com/LykkeCity/Lykke-TradingAPI-Examples/blob/master/src/Example.FollowPrices/Program.cs)

Create a client. You can skip the API key because you need just public API.

```csharp
  var client = new TradingApiClient("https://hft-apiv2-grpc.lykke.com", "");
```

Get prices snapshot

```csharp
  var priceRequest = new PricesRequest();
  
  // for example add filter by 3 instrument. You can keep AssetPairIds empty to receive all prices
  priceRequest.AssetPairIds.Add("BTCUSD");
  priceRequest.AssetPairIds.Add("BTCCHF");
  priceRequest.AssetPairIds.Add("BTCEUR");

  var prices = await client.PublicApi.GetPricesAsync(priceRequest);
  
  if (prices.error == null)
  {
    foreach(var price in prices.Payload)
    {
      Console.WriteLine($"{price.AssetPairId}: Ask={price.Ask}; Bid={price.Bid}; Time={price.Timestamp}");
    }
  }
  else
  {
    Console.WriteLine($"ERROR: {prices.error.Code}: {prices.error.Message}");
  }
```

Subscribe to price stream

```csharp
  var priceUpdateRequest = new PriceUpdatesRequest();
  priceUpdateRequest.AssetPairIds.Add("BTCUSD");
  priceUpdateRequest.AssetPairIds.Add("BTCCHF");
  priceUpdateRequest.AssetPairIds.Add("BTCEUR");

  Console.WriteLine("Subscribe to prices.");
  var priceStream = client.PublicApi.GetPriceUpdates(priceUpdateRequest);

  var token = new CancellationToken();
  while (await priceStream.ResponseStream.MoveNext(token))
  {
    var price = priceStream.ResponseStream.Current;

    Console.WriteLine($"{price.AssetPairId}: Ask={price.Ask}; Bid={price.Bid}; Time={price.Timestamp}");
  }
```
  
  
