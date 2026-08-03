BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS auth;
CREATE SCHEMA IF NOT EXISTS bitrix;
CREATE SCHEMA IF NOT EXISTS reporting;
CREATE SCHEMA IF NOT EXISTS audit;

CREATE OR REPLACE FUNCTION public.set_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$;

CREATE TABLE auth.users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email text NOT NULL UNIQUE,
    full_name text NOT NULL,
    status text NOT NULL DEFAULT 'active',
    external_identity_id text,
    last_login_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz,
    CONSTRAINT users_status_check CHECK (status IN ('active', 'invited', 'disabled'))
);

CREATE TRIGGER users_set_updated_at
BEFORE UPDATE ON auth.users
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE auth.roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code text NOT NULL UNIQUE,
    name text NOT NULL,
    description text,
    is_system boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TRIGGER roles_set_updated_at
BEFORE UPDATE ON auth.roles
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE auth.permissions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code text NOT NULL UNIQUE,
    name text NOT NULL,
    description text,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE auth.role_permissions (
    role_id uuid NOT NULL REFERENCES auth.roles(id) ON DELETE CASCADE,
    permission_id uuid NOT NULL REFERENCES auth.permissions(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE auth.user_roles (
    user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    role_id uuid NOT NULL REFERENCES auth.roles(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, role_id)
);

CREATE TABLE bitrix.connections (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name text NOT NULL,
    base_url text NOT NULL,
    auth_secret_name text NOT NULL,
    status text NOT NULL DEFAULT 'active',
    last_successful_sync_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT connections_status_check CHECK (status IN ('active', 'disabled'))
);

CREATE TRIGGER connections_set_updated_at
BEFORE UPDATE ON bitrix.connections
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.sync_runs (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    entity_type text NOT NULL,
    mode text NOT NULL,
    status text NOT NULL DEFAULT 'queued',
    started_at timestamptz,
    finished_at timestamptz,
    cursor_value text,
    records_read integer NOT NULL DEFAULT 0,
    records_written integer NOT NULL DEFAULT 0,
    error_message text,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT sync_runs_mode_check CHECK (mode IN ('full', 'incremental', 'retry')),
    CONSTRAINT sync_runs_status_check CHECK (status IN ('queued', 'running', 'succeeded', 'failed', 'cancelled'))
);

CREATE INDEX sync_runs_connection_entity_idx ON bitrix.sync_runs(connection_id, entity_type, created_at DESC);

CREATE TRIGGER sync_runs_set_updated_at
BEFORE UPDATE ON bitrix.sync_runs
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.sync_locks (
    lock_key text PRIMARY KEY,
    owner_id text NOT NULL,
    acquired_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL
);

CREATE TABLE bitrix.entity_types (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code text NOT NULL UNIQUE,
    name text NOT NULL,
    bitrix_method_prefix text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    sync_order integer NOT NULL DEFAULT 100,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TRIGGER entity_types_set_updated_at
BEFORE UPDATE ON bitrix.entity_types
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.areas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    slug text NOT NULL UNIQUE,
    name text NOT NULL,
    description text,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TRIGGER areas_set_updated_at
BEFORE UPDATE ON bitrix.areas
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.pipelines (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid REFERENCES bitrix.connections(id),
    area_id uuid REFERENCES bitrix.areas(id),
    slug text NOT NULL UNIQUE,
    name text NOT NULL,
    category_id integer NOT NULL UNIQUE,
    domain text NOT NULL DEFAULT 'general',
    is_active boolean NOT NULL DEFAULT true,
    sync_order integer NOT NULL DEFAULT 100,
    field_map jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX bitrix_pipelines_active_idx ON bitrix.pipelines(is_active, sync_order, category_id);

CREATE TRIGGER pipelines_set_updated_at
BEFORE UPDATE ON bitrix.pipelines
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.pipeline_stages (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    pipeline_id uuid NOT NULL REFERENCES bitrix.pipelines(id) ON DELETE CASCADE,
    bitrix_stage_id text NOT NULL,
    name text NOT NULL,
    sort_order integer,
    status_type text,
    raw_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (pipeline_id, bitrix_stage_id)
);

CREATE INDEX pipeline_stages_pipeline_idx ON bitrix.pipeline_stages(pipeline_id, sort_order);

CREATE TRIGGER pipeline_stages_set_updated_at
BEFORE UPDATE ON bitrix.pipeline_stages
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.custom_fields (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type_id uuid NOT NULL REFERENCES bitrix.entity_types(id) ON DELETE CASCADE,
    bitrix_field_code text NOT NULL,
    name text,
    value_type text,
    is_multiple boolean NOT NULL DEFAULT false,
    settings jsonb NOT NULL DEFAULT '{}'::jsonb,
    first_seen_at timestamptz NOT NULL DEFAULT now(),
    last_seen_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (entity_type_id, bitrix_field_code)
);

CREATE INDEX custom_fields_code_idx ON bitrix.custom_fields(bitrix_field_code);

CREATE TABLE bitrix.entity_custom_values (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    entity_type text NOT NULL,
    bitrix_id text NOT NULL,
    bitrix_field_code text NOT NULL,
    value_text text,
    value_number numeric(18, 4),
    value_date timestamptz,
    value_json jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, entity_type, bitrix_id, bitrix_field_code)
);

CREATE INDEX entity_custom_values_lookup_idx ON bitrix.entity_custom_values(entity_type, bitrix_field_code);
CREATE INDEX entity_custom_values_text_idx ON bitrix.entity_custom_values(bitrix_field_code, value_text);
CREATE INDEX entity_custom_values_number_idx ON bitrix.entity_custom_values(bitrix_field_code, value_number);

CREATE TRIGGER entity_custom_values_set_updated_at
BEFORE UPDATE ON bitrix.entity_custom_values
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.raw_payloads (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    pipeline_id uuid REFERENCES bitrix.pipelines(id),
    sync_run_id uuid REFERENCES bitrix.sync_runs(id),
    entity_type text NOT NULL,
    bitrix_id text NOT NULL,
    payload jsonb NOT NULL,
    payload_hash text NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, entity_type, bitrix_id, payload_hash)
);

CREATE INDEX raw_payloads_lookup_idx ON bitrix.raw_payloads(connection_id, entity_type, bitrix_id);
CREATE INDEX raw_payloads_received_idx ON bitrix.raw_payloads(received_at DESC);
CREATE INDEX raw_payloads_payload_gin_idx ON bitrix.raw_payloads USING gin(payload);

CREATE TABLE bitrix.entity_snapshots (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    pipeline_id uuid REFERENCES bitrix.pipelines(id),
    entity_type text NOT NULL,
    bitrix_id text NOT NULL,
    title text,
    assigned_by_bitrix_id text,
    stage_id text,
    category_id text,
    bitrix_created_at timestamptz,
    bitrix_updated_at timestamptz,
    core_data jsonb NOT NULL DEFAULT '{}'::jsonb,
    custom_fields jsonb NOT NULL DEFAULT '{}'::jsonb,
    raw_payload_id uuid REFERENCES bitrix.raw_payloads(id),
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_detected_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, entity_type, bitrix_id)
);

CREATE INDEX entity_snapshots_pipeline_idx ON bitrix.entity_snapshots(pipeline_id, entity_type, bitrix_updated_at DESC);
CREATE INDEX entity_snapshots_assigned_idx ON bitrix.entity_snapshots(connection_id, entity_type, assigned_by_bitrix_id);
CREATE INDEX entity_snapshots_core_gin_idx ON bitrix.entity_snapshots USING gin(core_data);
CREATE INDEX entity_snapshots_custom_gin_idx ON bitrix.entity_snapshots USING gin(custom_fields);

CREATE TRIGGER entity_snapshots_set_updated_at
BEFORE UPDATE ON bitrix.entity_snapshots
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    bitrix_id text NOT NULL,
    email text,
    full_name text,
    department text,
    active boolean NOT NULL DEFAULT true,
    raw_payload_id uuid REFERENCES bitrix.raw_payloads(id),
    bitrix_updated_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, bitrix_id)
);

CREATE INDEX bitrix_users_email_idx ON bitrix.users(email);

CREATE TRIGGER bitrix_users_set_updated_at
BEFORE UPDATE ON bitrix.users
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.companies (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    bitrix_id text NOT NULL,
    title text NOT NULL,
    assigned_by_bitrix_id text,
    industry text,
    raw_payload_id uuid REFERENCES bitrix.raw_payloads(id),
    bitrix_created_at timestamptz,
    bitrix_updated_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, bitrix_id)
);

CREATE INDEX bitrix_companies_title_idx ON bitrix.companies(title);

CREATE TRIGGER bitrix_companies_set_updated_at
BEFORE UPDATE ON bitrix.companies
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.contacts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    bitrix_id text NOT NULL,
    company_id uuid REFERENCES bitrix.companies(id),
    full_name text NOT NULL,
    email text,
    phone text,
    assigned_by_bitrix_id text,
    raw_payload_id uuid REFERENCES bitrix.raw_payloads(id),
    bitrix_created_at timestamptz,
    bitrix_updated_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, bitrix_id)
);

CREATE INDEX bitrix_contacts_company_idx ON bitrix.contacts(company_id);
CREATE INDEX bitrix_contacts_email_idx ON bitrix.contacts(email);

CREATE TRIGGER bitrix_contacts_set_updated_at
BEFORE UPDATE ON bitrix.contacts
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.deals (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    pipeline_id uuid REFERENCES bitrix.pipelines(id),
    bitrix_id text NOT NULL,
    title text NOT NULL,
    stage_id text,
    category_id text,
    company_id uuid REFERENCES bitrix.companies(id),
    contact_id uuid REFERENCES bitrix.contacts(id),
    assigned_by_bitrix_id text,
    opportunity numeric(18, 2),
    currency_id text,
    is_closed boolean NOT NULL DEFAULT false,
    raw_payload_id uuid REFERENCES bitrix.raw_payloads(id),
    bitrix_created_at timestamptz,
    bitrix_updated_at timestamptz,
    closed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, bitrix_id)
);

CREATE INDEX bitrix_deals_stage_idx ON bitrix.deals(connection_id, stage_id);
CREATE INDEX bitrix_deals_pipeline_idx ON bitrix.deals(pipeline_id, bitrix_updated_at DESC);
CREATE INDEX bitrix_deals_assigned_idx ON bitrix.deals(connection_id, assigned_by_bitrix_id);
CREATE INDEX bitrix_deals_company_idx ON bitrix.deals(company_id);
CREATE INDEX bitrix_deals_contact_idx ON bitrix.deals(contact_id);
CREATE INDEX bitrix_deals_updated_idx ON bitrix.deals(bitrix_updated_at DESC);

CREATE TRIGGER bitrix_deals_set_updated_at
BEFORE UPDATE ON bitrix.deals
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.activities (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    bitrix_id text NOT NULL,
    owner_type text,
    owner_bitrix_id text,
    type_id text,
    subject text,
    responsible_bitrix_id text,
    completed boolean NOT NULL DEFAULT false,
    deadline_at timestamptz,
    raw_payload_id uuid REFERENCES bitrix.raw_payloads(id),
    bitrix_created_at timestamptz,
    bitrix_updated_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, bitrix_id)
);

CREATE INDEX bitrix_activities_owner_idx ON bitrix.activities(connection_id, owner_type, owner_bitrix_id);
CREATE INDEX bitrix_activities_responsible_idx ON bitrix.activities(connection_id, responsible_bitrix_id);
CREATE INDEX bitrix_activities_deadline_idx ON bitrix.activities(deadline_at);

CREATE TRIGGER bitrix_activities_set_updated_at
BEFORE UPDATE ON bitrix.activities
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.tasks (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    bitrix_id text NOT NULL,
    related_entity_type text,
    related_bitrix_id text,
    title text NOT NULL,
    status text,
    priority text,
    created_by_bitrix_id text,
    responsible_bitrix_id text,
    accomplices jsonb NOT NULL DEFAULT '[]'::jsonb,
    auditors jsonb NOT NULL DEFAULT '[]'::jsonb,
    deadline_at timestamptz,
    closed_at timestamptz,
    raw_payload_id uuid REFERENCES bitrix.raw_payloads(id),
    bitrix_created_at timestamptz,
    bitrix_updated_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, bitrix_id)
);

CREATE INDEX bitrix_tasks_related_idx ON bitrix.tasks(connection_id, related_entity_type, related_bitrix_id);
CREATE INDEX bitrix_tasks_responsible_idx ON bitrix.tasks(connection_id, responsible_bitrix_id);
CREATE INDEX bitrix_tasks_deadline_idx ON bitrix.tasks(deadline_at);

CREATE TRIGGER bitrix_tasks_set_updated_at
BEFORE UPDATE ON bitrix.tasks
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE bitrix.timeline_comments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL REFERENCES bitrix.connections(id),
    bitrix_id text NOT NULL,
    entity_type text NOT NULL,
    entity_bitrix_id text NOT NULL,
    author_bitrix_id text,
    comment_text text,
    raw_payload_id uuid REFERENCES bitrix.raw_payloads(id),
    bitrix_created_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (connection_id, bitrix_id)
);

CREATE INDEX timeline_comments_entity_idx ON bitrix.timeline_comments(connection_id, entity_type, entity_bitrix_id, bitrix_created_at DESC);

CREATE TRIGGER timeline_comments_set_updated_at
BEFORE UPDATE ON bitrix.timeline_comments
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE reporting.report_definitions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code text NOT NULL UNIQUE,
    name text NOT NULL,
    description text,
    query_key text NOT NULL,
    parameters_schema jsonb NOT NULL DEFAULT '{}'::jsonb,
    status text NOT NULL DEFAULT 'draft',
    owner_user_id uuid REFERENCES auth.users(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz,
    CONSTRAINT report_definitions_status_check CHECK (status IN ('draft', 'published', 'archived'))
);

CREATE TRIGGER report_definitions_set_updated_at
BEFORE UPDATE ON reporting.report_definitions
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE reporting.report_access (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    report_definition_id uuid NOT NULL REFERENCES reporting.report_definitions(id) ON DELETE CASCADE,
    user_id uuid REFERENCES auth.users(id) ON DELETE CASCADE,
    role_id uuid REFERENCES auth.roles(id) ON DELETE CASCADE,
    access_level text NOT NULL DEFAULT 'viewer',
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz,
    CONSTRAINT report_access_subject_check CHECK (
        (user_id IS NOT NULL AND role_id IS NULL)
        OR (user_id IS NULL AND role_id IS NOT NULL)
    ),
    CONSTRAINT report_access_level_check CHECK (access_level IN ('viewer', 'editor', 'owner'))
);

CREATE UNIQUE INDEX report_access_user_unique_idx
ON reporting.report_access(report_definition_id, user_id)
WHERE user_id IS NOT NULL;

CREATE UNIQUE INDEX report_access_role_unique_idx
ON reporting.report_access(report_definition_id, role_id)
WHERE role_id IS NOT NULL;

CREATE TABLE reporting.report_runs (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    report_definition_id uuid NOT NULL REFERENCES reporting.report_definitions(id),
    requested_by_user_id uuid REFERENCES auth.users(id),
    status text NOT NULL DEFAULT 'queued',
    parameters jsonb NOT NULL DEFAULT '{}'::jsonb,
    started_at timestamptz,
    finished_at timestamptz,
    row_count integer,
    error_message text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT report_runs_status_check CHECK (status IN ('queued', 'running', 'succeeded', 'failed', 'cancelled'))
);

CREATE INDEX report_runs_definition_idx ON reporting.report_runs(report_definition_id, created_at DESC);

CREATE TRIGGER report_runs_set_updated_at
BEFORE UPDATE ON reporting.report_runs
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

CREATE TABLE reporting.report_snapshots (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    report_run_id uuid NOT NULL REFERENCES reporting.report_runs(id) ON DELETE CASCADE,
    storage_uri text,
    data jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT report_snapshots_content_check CHECK (storage_uri IS NOT NULL OR data IS NOT NULL)
);

CREATE TABLE audit.events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_user_id uuid REFERENCES auth.users(id),
    event_type text NOT NULL,
    entity_type text,
    entity_id uuid,
    ip_address inet,
    user_agent text,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX audit_events_actor_idx ON audit.events(actor_user_id, created_at DESC);
CREATE INDEX audit_events_type_idx ON audit.events(event_type, created_at DESC);
CREATE INDEX audit_events_entity_idx ON audit.events(entity_type, entity_id);

INSERT INTO auth.roles (code, name, description, is_system)
VALUES
    ('admin', 'Administrador', 'Acceso administrativo completo.', true),
    ('report_manager', 'Gestor de informes', 'Puede crear y administrar informes.', true),
    ('report_viewer', 'Lector de informes', 'Puede consultar informes asignados.', true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO auth.permissions (code, name, description)
VALUES
    ('users.manage', 'Administrar usuarios', 'Crear, actualizar y desactivar usuarios.'),
    ('roles.manage', 'Administrar roles', 'Crear y modificar roles y permisos.'),
    ('reports.view', 'Ver informes', 'Consultar informes asignados.'),
    ('reports.manage', 'Administrar informes', 'Crear, publicar y archivar informes.'),
    ('bitrix.sync.run', 'Ejecutar sincronizacion Bitrix', 'Iniciar sincronizaciones con Bitrix.'),
    ('bitrix.sync.view', 'Ver sincronizaciones Bitrix', 'Consultar estado e historial de sincronizaciones.')
ON CONFLICT (code) DO NOTHING;

INSERT INTO auth.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM auth.roles r
CROSS JOIN auth.permissions p
WHERE r.code = 'admin'
ON CONFLICT DO NOTHING;

INSERT INTO auth.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM auth.roles r
JOIN auth.permissions p ON p.code IN ('reports.view', 'reports.manage', 'bitrix.sync.view')
WHERE r.code = 'report_manager'
ON CONFLICT DO NOTHING;

INSERT INTO auth.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM auth.roles r
JOIN auth.permissions p ON p.code = 'reports.view'
WHERE r.code = 'report_viewer'
ON CONFLICT DO NOTHING;

INSERT INTO bitrix.pipelines (slug, name, category_id, domain, sync_order)
VALUES
    ('rch_comercial', 'RCH Comercial', 8, 'comercial', 10),
    ('rch_operativa', 'RCH Operativa', 10, 'operaciones', 20),
    ('pnnc_comercial', 'PNNC Comercial', 26, 'comercial', 30),
    ('pnnc_operativa', 'PNNC Operativa', 28, 'operaciones', 40)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO bitrix.areas (slug, name, description)
VALUES
    ('cartera', 'Cartera', 'Gestion de cartera, recaudo, mora y pagos.'),
    ('juridico', 'Juridico', 'Gestion juridica asociada a negociaciones.'),
    ('comercial', 'Comercial', 'Seguimiento comercial y oportunidades.'),
    ('operaciones', 'Operaciones', 'Gestion operativa y tareas.')
ON CONFLICT (slug) DO NOTHING;

UPDATE bitrix.pipelines p
SET area_id = a.id
FROM bitrix.areas a
WHERE p.domain = a.slug
  AND p.area_id IS NULL;

INSERT INTO bitrix.connections (name, base_url, auth_secret_name, status)
VALUES ('Bitrix principal', 'BITRIX_WEBHOOK_URL', 'BITRIX_WEBHOOK_URL', 'active')
ON CONFLICT DO NOTHING;

INSERT INTO bitrix.entity_types (code, name, bitrix_method_prefix, sync_order)
VALUES
    ('user', 'Usuarios', 'user', 10),
    ('deal_category_stage', 'Etapas de negocios', 'crm.dealcategory.stage', 20),
    ('deal', 'Negocios', 'crm.deal', 30),
    ('contact', 'Contactos', 'crm.contact', 40),
    ('company', 'Companias', 'crm.company', 50),
    ('activity', 'Actividades', 'crm.activity', 60),
    ('task', 'Tareas', 'tasks.task', 70),
    ('timeline_comment', 'Comentarios de timeline', 'crm.timeline.comment', 80)
ON CONFLICT (code) DO NOTHING;

COMMIT;
