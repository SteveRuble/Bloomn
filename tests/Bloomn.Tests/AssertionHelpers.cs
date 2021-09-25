using FluentAssertions;
using JetBrains.Annotations;

namespace Bloomn.Tests
{
    public static class AssertionHelpers
    {
        [ContractAnnotation("item:null=>halt")]
        public static void ShouldNotBeNull([System.Diagnostics.CodeAnalysis.NotNull]
            this object item)
        {
            item.Should().NotBeNull();
        }
    }
}