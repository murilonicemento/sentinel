db = db.getSiblingDB("IngestionReadDatabase");

db.createUser({
    user: "app_user",
    pwd: "app_password",
    roles: [
        { role: "readWrite", db: "IngestionReadDatabase" }
    ]
});

