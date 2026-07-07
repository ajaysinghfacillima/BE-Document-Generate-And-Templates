# Database Deployment and Rollback

1. Apply migrations in ascending order using `apply-migrations.sql`.
2. Roll back using `rollback-migrations.sql` in reverse order.
3. Scripts are re-runnable and guarded with existence checks.
