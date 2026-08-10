CREATE TABLE IF NOT EXISTS bitrix.outgoing_webhook_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type text NOT NULL DEFAULT 'deal',
    bitrix_id text NOT NULL,
    event_name text NOT NULL,
    portal_domain text,
    payload jsonb NOT NULL,
    status text NOT NULL DEFAULT 'pending',
    event_count integer NOT NULL DEFAULT 1,
    attempts integer NOT NULL DEFAULT 0,
    first_seen_at timestamptz NOT NULL DEFAULT now(),
    last_seen_at timestamptz NOT NULL DEFAULT now(),
    processed_at timestamptz,
    last_error text,
    sync_run_id uuid REFERENCES bitrix.sync_runs(id),
    CONSTRAINT outgoing_webhook_events_status_check CHECK (status IN ('pending', 'processing', 'processed', 'failed'))
);

CREATE UNIQUE INDEX IF NOT EXISTS outgoing_webhook_events_entity_bitrix_idx
ON bitrix.outgoing_webhook_events(entity_type, bitrix_id);

CREATE INDEX IF NOT EXISTS outgoing_webhook_events_status_seen_idx
ON bitrix.outgoing_webhook_events(status, last_seen_at);
