using Xunit;

// Global collection definition to serialize EventBus tests that mutate static state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PowNet.Test
{
    [CollectionDefinition("EventBusSerial")] public class EventBusSerialCollection : ICollectionFixture<EventBusSerialFixture> { }
    public class EventBusSerialFixture { }
}
