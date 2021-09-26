using Xunit.Abstractions;

namespace Bloomn.Tests.Examples
{
    public class ExampleProgram
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public ExampleProgram(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public void WriteLine(string line)
        {
            _testOutputHelper.WriteLine(line);
        }
    }
}