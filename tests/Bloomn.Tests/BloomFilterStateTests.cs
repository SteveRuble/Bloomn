using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Bloomn.Tests
{
    public class BloomFilterStateTests
    {
        [Fact]
        public void CanRoundTripStateWithoutChildren()
        {
            var expected = new BloomFilterState
            {
                Parameters = new BloomFilterParameters("id1")
                {
                    Dimensions = new BloomFilterDimensions(),
                    Scaling = new BloomFilterScaling(),
                    HashAlgorithm = "test"
                },
                Count = 1234,
                BitArrays = new List<byte[]>
                {
                    Encoding.ASCII.GetBytes("some bytes"),
                    Encoding.ASCII.GetBytes("some more bytes")
                }
            };

            var serialized = expected.Serialize();

            var actual = BloomFilterState.Deserialize(serialized);

            actual.Should().BeEquivalentTo(expected);
        }


        [Fact]
        public void CanRoundTripStateWithChildren()
        {
            var expected = new BloomFilterState
            {
                Parameters = new BloomFilterParameters("id1")
                {
                    Dimensions = new BloomFilterDimensions(),
                    Scaling = new BloomFilterScaling(),
                    HashAlgorithm = "test"
                },
                Count = 1234,
                Children = new List<BloomFilterState>
                {
                    new()
                    {
                        Parameters = new BloomFilterParameters("id1.1")
                        {
                            Dimensions = new BloomFilterDimensions(),
                            Scaling = new BloomFilterScaling(),
                            HashAlgorithm = "test"
                        },
                        Count = 1234,
                        BitArrays = new List<byte[]>
                        {
                            Encoding.ASCII.GetBytes("some bytes"),
                            Encoding.ASCII.GetBytes("some more bytes")
                        }
                    },
                    new()
                    {
                        Parameters = new BloomFilterParameters("id1.2")
                        {
                            Dimensions = new BloomFilterDimensions(),
                            Scaling = new BloomFilterScaling(),
                            HashAlgorithm = "test"
                        },
                        Count = 1234,
                        BitArrays = new List<byte[]>
                        {
                            Encoding.ASCII.GetBytes("some bytes"),
                            Encoding.ASCII.GetBytes("some more bytes")
                        }
                    }
                }
            };

            var serialized = expected.Serialize();

            var actual = BloomFilterState.Deserialize(serialized);

            actual.Should().BeEquivalentTo(expected);
        }
    }
}