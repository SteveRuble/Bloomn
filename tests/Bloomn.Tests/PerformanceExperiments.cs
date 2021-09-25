using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Bloomn.Tests
{
    public class PerformanceExperiments
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public PerformanceExperiments(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public Stopwatch RunInterface(int reps)
        {
            ITestAdder viaInterface = new TestAdder();
            var timer = Stopwatch.StartNew();
            for (var i = 0; i < reps;) i = viaInterface.Add(1, i);
            timer.Stop();
            return timer;
        }

        public Stopwatch RunDerived(int reps)
        {
            TestAdderBase sut = new TestAdderDerived();
            var timer = Stopwatch.StartNew();
            for (var i = 0; i < reps;) i = sut.Add(1, i);
            timer.Stop();
            return timer;
        }

        public Stopwatch RunFunc(int reps)
        {
            Func<int, int, int> sut = (a, b) => a + b;

            var timer = Stopwatch.StartNew();
            for (var i = 0; i < reps;) i = sut(1, i);
            timer.Stop();
            return timer;
        }

        [Fact]
        public void InterfaceVsFuncTime()
        {
            // warmup
            RunFunc(1);
            RunDerived(1);
            RunInterface(1);

            // run
            var reps = 100000;

            var funcTimer = RunFunc(reps);
            var derivedTimer = RunDerived(reps);
            var interfaceTimer = RunInterface(reps);

            _testOutputHelper.WriteLine($"Interface time with {reps}: {interfaceTimer.ElapsedTicks}");
            _testOutputHelper.WriteLine($"Derived time with {reps}: {derivedTimer.ElapsedTicks}");
            _testOutputHelper.WriteLine($"Func time with {reps}: {funcTimer.ElapsedTicks}");
        }

        public interface ITestAdder
        {
            int Add(int a, int b);
        }

        public class TestAdder : ITestAdder
        {
            public int Add(int a, int b)
            {
                return a + b;
            }
        }

        public abstract class TestAdderBase
        {
            public abstract int Add(int a, int b);
        }

        public class TestAdderDerived : TestAdderBase
        {
            public override int Add(int a, int b)
            {
                return a + b;
            }
        }
    }
}