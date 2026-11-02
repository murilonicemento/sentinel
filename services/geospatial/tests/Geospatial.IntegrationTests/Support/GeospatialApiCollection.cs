namespace Geospatial.IntegrationTests.Support;

// Program uses Serilog's bootstrap logger, which can be frozen only once per process, so every test class shares a
// single host instead of creating one factory per class.
[CollectionDefinition(Name)]
public sealed class GeospatialApiCollection : ICollectionFixture<GeospatialApiFactory>
{
    public const string Name = "geospatial-api";
}
