# Database

PostgreSQL 17 + pgvector 0.8.2. All schema changes go through EF Core migrations.

## Run the database

```bash
docker run -d --name raghub-postgres \
  -e POSTGRES_PASSWORD=raghub_dev \
  -e POSTGRES_DB=raghub \
  -p 5433:5432 \
  pgvector/pgvector:pg17
```

Port 5433 avoids conflict with any existing Postgres on 5432.

## Apply migrations

```bash
dotnet ef database update \
  --project src/RagHub.Infrastructure \
  --startup-project src/RagHub.API
```

Connection string is read from `src/RagHub.API/appsettings.Development.json`:
```
Host=localhost;Port=5433;Database=raghub;Username=postgres;Password=raghub_dev
```

## Create a new migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/RagHub.Infrastructure \
  --startup-project src/RagHub.API \
  --output-dir Persistence/Migrations
```

## Verify schema

```bash
docker exec raghub-postgres psql -U postgres -d raghub -c "\dt"
docker exec raghub-postgres psql -U postgres -d raghub -c "\d chunks"
```

## Notes

- The `chunks.content_tsv` column is a generated `tsvector` — populated automatically from `content` on insert/update.
- The `chunks.embedding` column is `vector(1024)` — populated by the Worker during indexing (Phase 2).
- `documents.owner` and `documents.department` are nullable placeholders for future ACL (Phase 2, unused in POC).
