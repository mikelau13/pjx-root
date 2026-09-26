-- Runs once, on first initialisation of the pjx-pgdata volume. POSTGRES_DB
-- creates pjx_calendar; this creates the second database. Mirrors
-- helm-pjx/templates/pjx-postgres.yaml — keep them identical.
CREATE DATABASE pjx_identity OWNER pjx;