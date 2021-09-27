# Bloomn Bloom Filter for .NET

Bloomn provides a modern, high performance bloom filter implementation.

## Features

- Provides a zero-allocation API for adding and checking keys.
- Bloom filter state can be exported, serialized, and imported.
- Integrates with standard .NET dependency injection framework and configuration system.
- Supports scalable bloom filters for scenarios where set size is unknown.
- Thread safe.
- High test coverage.
- Default key hasher handles `string`, `byte[]`, `Guid`, and numeric types.


## Examples

See [tests/Bloomn.Tests/Examples](tests/Bloomn.Tests/Examples)

### Using Service Provider
```c#
using Bloomn;
using Bloomn.Extensions;

// ...

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
    
    // For this example, we'll populate the filter with prime numbers using some method
    for (var prime in EnumeratePrimeNumbers(1000))
    {
        filter.Add(prime);
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
```

You can also create a builder directly, like this:

```c#
var filter = BloomFilter.Builder<int>()
                        .WithOptions(x => x.WithCapacityAndFalsePositiveProbability(1000, 0.02)
                                           .WithScaling(4, 0.9))
                        .Build();
```

For maximum flexibility and performance you can check whether a key is present
and defer the add until later (so you only need to calculate the hash once).
This is a tiny bit faster than checking and adding as separate operations. 
I designed this API before I figured out some performance improvements that
made it barely useful.

It's important to dispose of the check result to avoid memory leaks.

```c#
using(var preparedCheck = sut.CheckAndPrepareAdd(KeyIndex))
{
    if (prepared.IsNotPresent)
    {
        try 
        {
            //  do work ...
            
            // ...and if it succeeds update the filter
            prepared.Add();
        }
        catch
        {
            // filter won't be updated
        }
    }
}
```
