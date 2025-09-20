DO
$$
BEGIN
   IF NOT EXISTS (
      SELECT FROM pg_database WHERE datname = 'ingestion'
   ) THEN
      CREATE DATABASE ingestion;
END IF;
END
$$;