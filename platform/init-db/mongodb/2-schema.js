db = db.getSiblingDB("IngestionReadDatabase");

db.createCollection("climaticEvents");
db.createCollection("disasterEvents");

db.climaticEvents.createIndex({EventId: 1}, {unique: true});
db.climaticEvents.createIndex({EventType: 1});
db.climaticEvents.createIndex({CollectedAt: -1});

db.disasterEvents.createIndex({EventId: 1}, {unique: true});
db.disasterEvents.createIndex({EventType: 1});
db.disasterEvents.createIndex({CollectedAt: -1});

db.climaticEvents.insertMany([
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

db.disasterEvents.insertMany([
    {
        EventId: UUID(),
        EventType: "Wildfire",
        Intensity: 12.5,
        Latitude: -23.5505,
        Longitude: -46.6333,
        CollectedAt: ISODate()
    },
    {
        EventId: UUID(),
        EventType: "Earthquake",
        Intensity: 40.2,
        Latitude: -22.9068,
        Longitude: -43.1729,
        CollectedAt: ISODate()
    }
]);