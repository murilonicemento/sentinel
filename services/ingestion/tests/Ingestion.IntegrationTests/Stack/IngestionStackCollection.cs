using Xunit;

namespace Ingestion.IntegrationTests.Stack;

/// <summary>One set of containers for every end-to-end test class.</summary>
[CollectionDefinition(Name)]
public sealed class IngestionStackCollection : ICollectionFixture<IngestionStackFactory>
{
    public const string Name = "ingestion-stack";
}
