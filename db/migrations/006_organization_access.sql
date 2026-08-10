ALTER TABLE auth.users
ADD COLUMN IF NOT EXISTS bitrix_user_id text;

CREATE INDEX IF NOT EXISTS auth_users_bitrix_user_id_idx
ON auth.users(bitrix_user_id);

CREATE TABLE IF NOT EXISTS reporting.organization_access (
    department_id text PRIMARY KEY,
    role_label text NOT NULL DEFAULT 'leader',
    visible_reports text[] NOT NULL DEFAULT ARRAY[]::text[],
    visible_blocks text[] NOT NULL DEFAULT ARRAY[]::text[],
    user_id uuid NULL REFERENCES auth.users(id) ON DELETE SET NULL,
    updated_at timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE reporting.organization_access
    ADD COLUMN IF NOT EXISTS user_id uuid NULL REFERENCES auth.users(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_organization_access_user
    ON reporting.organization_access(user_id);

CREATE TABLE IF NOT EXISTS reporting.user_report_block_settings (
    user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    report_code text NOT NULL,
    visible_blocks text[] NOT NULL DEFAULT ARRAY[]::text[],
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, report_code)
);
