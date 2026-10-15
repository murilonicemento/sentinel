
# Database Rules

## Purpose
Ensure safe, consistent, and efficient data access.

## Database Strategy
- Database engine: TODO
- Data access technology: TODO
- Schema conventions: TODO

## Data Modeling
- Table and column naming: TODO
- Primary and foreign key conventions: TODO
- Constraints and indexes: TODO
- Relationship modeling: TODO

## Migrations
- Migration tool: TODO
- Migration naming: TODO
- Rollback strategy: TODO
- Production migration policy: TODO

## Data Integrity
- Use database constraints where appropriate.
- Use transactions when operations require atomicity.
- Never assume concurrent requests cannot modify the same data.
- Avoid destructive schema changes without an explicit migration plan.

## Query Performance
- Prevent N+1 query patterns.
- Avoid retrieving unnecessary columns and records.
- Review indexes against actual query patterns.

## Validation
- Migration tests: TODO
- Data integrity checks: TODO