using System.IO;
using Bloomn.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Bloomn.Tests.Examples
{
    public class ServiceProviderExample : ExampleProgram
    {
        public ServiceProviderExample(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }


        [Fact]
        public void Execute()
        {
            Directory.CreateDirectory("./Data");
            const string filePath = "./Data/filter.json";

            // Configure the settings for the default bloom filter here:
            var serviceProvider = new ServiceCollection()
                                  .AddBloomFilters<int>(c =>
                                  {
                                      c.WithDefaultProfile(d =>
                                          d.WithCapacityAndFalsePositiveProbability(1000, 0.02)
                                           .WithScaling(4, 0.9));
                                  })
                                  .BuildServiceProvider();

            IBloomFilter<int> filter;

            var builder = serviceProvider.GetRequiredService<IBloomFilterBuilder<int>>();

            if (File.Exists(filePath))
            {
                // If a serialized state exists, pass it to the builder here.
                // The builder will verify that the serialized state matches the
                // builder configurations provided.
                var serializedState = File.ReadAllText(filePath);
                var state = BloomFilterState.Deserialize(serializedState);
                filter = builder.WithState(state).Build();
            }
            else
            {
                // If no serialized state exists, we can build and populate the filter:
                filter = builder.Build();
                filter.Add(2);
                filter.Add(3);

                for (var i = 3; i < 1000; i = MathHelpers.GetNextPrimeNumber(i + 1))
                {
                    filter.Add(i);
                }

                // Export the state and save it for next time:
                var state = filter.GetState();
                var serializedState = state.Serialize();
                File.WriteAllText(filePath, serializedState);
            }

            for (var i = 0; i < 1000; i++)
            {
                if (filter.IsNotPresent(i))
                {
                    WriteLine($"Not prime: {i}");
                }
                else
                {
                    WriteLine($"Probably prime: {i}");
                }
            }
        }
    }
}