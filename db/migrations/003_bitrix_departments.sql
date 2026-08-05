BEGIN;

CREATE TABLE IF NOT EXISTS bitrix.departments (
    id bigint PRIMARY KEY,
    name text NOT NULL,
    parent_id bigint REFERENCES bitrix.departments(id),
    head_bitrix_id text,
    sort_order integer NOT NULL DEFAULT 0,
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS bitrix_departments_parent_idx
ON bitrix.departments(parent_id);

COMMIT;
