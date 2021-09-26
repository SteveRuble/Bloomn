using System.IO;
using Bloomn.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Bloomn.Tests.Examples
{
    public class NewBuilderExample : ExampleProgram
    {
        public NewBuilderExample(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }


        [Fact]
        public void Execute()
        {
            Directory.CreateDirectory("./Data");
            const string filePath = "./Data/filter.json";

            IBloomFilter<int> filter;

            if (File.Exists(filePath))
            {
                var serializedState = File.ReadAllText(filePath);
                var state = BloomFilterState.Deserialize(serializedState);
                // You can the filter directly from the state, without configuration.
                // The state will be used to configure it.
                filter = BloomFilter.Builder<int>()
                                    .WithState(state)
                                    .Build();
            }
            else
            {
                filter = BloomFilter.Builder<int>()
                                    .WithOptions(x => x.WithCapacityAndFalsePositiveProbability(1000, 0.02)
                                                       .WithScaling(4, 0.9))
                                    .Build();
                filter.Add(2);
                filter.Add(3);

                for (var i = 3; i < 1000; i = MathHelpers.GetNextPrimeNumber(i + 1))
                {
                    filter.Add(i);
                }

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