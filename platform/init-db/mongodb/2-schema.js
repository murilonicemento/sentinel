db = db.getSiblingDB("IngestionReadDatabase");

db.createCollection("events");

db.events.createIndex({EventId: 1}, {unique: true});
db.events.createIndex({EventType: 1});
db.events.createIndex({CollectedAt: -1});

db.events.insertMany([
    {
        EventId: UUID(),
        EventType: "TemperatureAnomaly",
        Intensity: 12.5,
        Latitude: -23.5505,
        Longitude: -46.6333,
        CollectedAt: ISODate()
    },
    {
        EventId: UUID(),
        EventType: "HumidityAnomaly",
        Intensity: 40.2,
        Latitude: -22.9068,
        Longitude: -43.1729,
        CollectedAt: ISODate()
    }
]);