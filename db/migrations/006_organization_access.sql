CREATE TABLE IF NOT EXISTS reporting.organization_access (
    department_id text PRIMARY KEY,
    role_label text NOT NULL DEFAULT 'viewer',
    visible_reports text[] NOT NULL DEFAULT ARRAY[]::text[],
    visible_blocks text[] NOT NULL DEFAULT ARRAY[]::text[],
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS reporting.user_report_block_settings (
    user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    report_code text NOT NULL,
    visible_blocks text[] NOT NULL DEFAULT ARRAY[]::text[],
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, report_code)
);
