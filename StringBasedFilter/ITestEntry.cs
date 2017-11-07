using System;
using JetBrains.Annotations;

namespace StringBasedFilter {
    public interface ITestEntry {
        bool Test([CanBeNull] string value);
        bool Test(double value);
        bool Test(bool value);
        bool Test(TimeSpan value);
        bool Test(DateTime value);
    }
}
