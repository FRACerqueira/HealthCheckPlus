// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal;

namespace HealthCheckPlusTests
{
    // InternalCast.To<T> backs every place this library downcasts a value resolved from DI
    // (HealthCheckService -> DefaultHealthCheckServicePlus, IStateHealthChecksPlus ->
    // CacheHealthCheckPlus) back to the concrete type AddHealthChecksPlus() actually registers.
    // A raw C# cast at those call sites used to fail (if a consumer decorated/replaced one of
    // those registrations) with a generic InvalidCastException naming only the two types
    // involved, not which registration was actually at fault.
    public class InternalCastTests
    {
        private sealed class Expected
        {
        }

        private sealed class Unexpected
        {
        }

        [Fact]
        public void To_ShouldReturnTheValue_WhenItIsAlreadyTheExpectedType()
        {
            var value = new Expected();

            var result = InternalCast.To<Expected>(value, "the registered thing");

            Assert.Same(value, result);
        }

        [Fact]
        public void To_ShouldThrowClearException_NamingTheRegistrationAndTheActualType_WhenTheTypeDoesNotMatch()
        {
            var value = new Unexpected();

            var ex = Assert.Throws<InvalidOperationException>(() => InternalCast.To<Expected>(value, "the registered thing"));

            Assert.Contains("the registered thing", ex.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(Expected), ex.Message, StringComparison.Ordinal);
            Assert.Contains(typeof(Unexpected).FullName!, ex.Message, StringComparison.Ordinal);
        }

        // Regression test: `value is T typed` is false for null too, so the failure branch used
        // to call value.GetType() on a null value, throwing an unrelated NullReferenceException
        // instead of the clear InvalidOperationException this method exists to give. Unreachable
        // from this class's own 5 call sites today (all pass a GetRequiredService<T>() result),
        // but the defensive check should not rely on that staying true forever.
        [Fact]
        public void To_ShouldThrowClearException_NotNullReferenceException_WhenValueIsNull()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => InternalCast.To<Expected>(null!, "the registered thing"));

            Assert.Contains("the registered thing", ex.Message, StringComparison.Ordinal);
            Assert.Contains("null", ex.Message, StringComparison.Ordinal);
        }
    }
}
