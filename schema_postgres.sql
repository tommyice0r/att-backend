-- ==============================================================================
-- SCHEMA COMPLETO DE POSTGRESQL PARA DOKPLOY / DOCKER - ATT BOT SYSTEM
-- ==============================================================================
-- Este script crea TODAS las tablas, índices, funciones, procedimientos almacenados
-- y semillas iniciales requeridas por el Bot, el Backend C# y el Panel de Control.
-- Base de Datos objetivo: ATT (o CLIENTACCESS)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. TABLA: client_access_records (Sistema Central de Licencias)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS client_access_records (
    access_id SERIAL PRIMARY KEY,
    client_name VARCHAR(150) NOT NULL,
    access_key VARCHAR(100) NOT NULL UNIQUE,
    app_code VARCHAR(50) NOT NULL DEFAULT 'att-bot',
    access_status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    is_master BOOLEAN NOT NULL DEFAULT FALSE,
    last_payment_at TIMESTAMPTZ NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMPTZ NOT NULL,
    grace_days INT NOT NULL DEFAULT 3,
    late_fee_per_day NUMERIC(10, 2) NOT NULL DEFAULT 500.00,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NULL DEFAULT CURRENT_TIMESTAMP,
    manual_allow_until TIMESTAMPTZ NULL,
    manual_allow_reason VARCHAR(300) NULL
);

CREATE INDEX IF NOT EXISTS idx_client_access_key ON client_access_records(access_key);
CREATE INDEX IF NOT EXISTS idx_client_access_app ON client_access_records(access_key, app_code);

-- ------------------------------------------------------------------------------
-- 2. TABLA: npanxx (Mapeo de Prefijos Telefónicos ATT)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS npanxx (
    id SERIAL PRIMARY KEY,
    npanxx VARCHAR(6) NOT NULL UNIQUE,
    npa VARCHAR(3) NOT NULL,
    nxx VARCHAR(3) NOT NULL,
    state_code VARCHAR(2) NOT NULL,
    state_name VARCHAR(50) NOT NULL,
    att_wireless_entity VARCHAR(200),
    att_wireless_ocn VARCHAR(20),
    att_landline_entity VARCHAR(200),
    cricket_entity VARCHAR(200),
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_npanxx_lookup ON npanxx(npanxx);
CREATE INDEX IF NOT EXISTS idx_npanxx_state ON npanxx(state_code);

-- ------------------------------------------------------------------------------
-- 3. TABLA: sequences (Secuencias NPA-NXX por Cliente)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sequences (
    id SERIAL PRIMARY KEY,
    access_id INT NOT NULL REFERENCES client_access_records(access_id) ON DELETE CASCADE,
    sequence VARCHAR(6) NOT NULL,
    state_code VARCHAR(2),
    state_name VARCHAR(50),
    total_leads INT DEFAULT 0,
    status VARCHAR(20) DEFAULT 'pending', -- pending, processing, completed, failed
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS idx_sequences_access_status ON sequences(access_id, status);

-- ------------------------------------------------------------------------------
-- 4. TABLA: leads (Números Telefónicos Generados / Depurados)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS leads (
    id SERIAL PRIMARY KEY,
    access_id INT NOT NULL REFERENCES client_access_records(access_id) ON DELETE CASCADE,
    sequence_id INT NULL REFERENCES sequences(id) ON DELETE SET NULL,
    phone_number VARCHAR(15) NOT NULL,
    full_name VARCHAR(200),
    address VARCHAR(500),
    zip_code VARCHAR(10),
    state_code VARCHAR(2),
    source VARCHAR(50) DEFAULT 'generator', -- generator, upload, manual
    status VARCHAR(20) DEFAULT 'pending',  -- pending, processing, hit, bad, blocked
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    processed_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS idx_leads_access_status ON leads(access_id, status);
CREATE INDEX IF NOT EXISTS idx_leads_phone ON leads(phone_number);

-- ------------------------------------------------------------------------------
-- 5. TABLA: hits (Números Elegibles Detectados en ATT)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS hits (
    id SERIAL PRIMARY KEY,
    access_id INT NOT NULL REFERENCES client_access_records(access_id) ON DELETE CASCADE,
    lead_id INT NULL REFERENCES leads(id) ON DELETE SET NULL,
    phone_number VARCHAR(15) NOT NULL,
    full_name VARCHAR(200),
    address VARCHAR(500),
    zip_code VARCHAR(10),
    device_message VARCHAR(500),
    device_type VARCHAR(50),
    hit_type VARCHAR(50), -- HIT, HIT_PREVIEW
    profile_raw TEXT,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_hits_access ON hits(access_id);

-- ------------------------------------------------------------------------------
-- 6. TABLA: bot_sessions (Estado en Vivo de los Workers del Bot)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS bot_sessions (
    id SERIAL PRIMARY KEY,
    worker_id INT NOT NULL,
    access_id INT NULL REFERENCES client_access_records(access_id) ON DELETE SET NULL,
    profile_id_gologin VARCHAR(100),
    status VARCHAR(20) DEFAULT 'idle', -- idle, running, blocked, error
    leads_processed INT DEFAULT 0,
    hits_found INT DEFAULT 0,
    last_heartbeat TIMESTAMPTZ,
    started_at TIMESTAMPTZ,
    error_message TEXT,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- ------------------------------------------------------------------------------
-- 7. TABLA: global_config (Configuraciones de Sistema / Proxies)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS global_config (
    id SERIAL PRIMARY KEY,
    key VARCHAR(100) NOT NULL UNIQUE,
    value TEXT,
    description VARCHAR(500),
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- ------------------------------------------------------------------------------
-- 8. TABLA: access_logs (Auditoría Central de Accesos y Verificaciones)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS access_logs (
    log_id BIGSERIAL PRIMARY KEY,
    access_key VARCHAR(100) NOT NULL,
    client_name VARCHAR(150),
    machine_name VARCHAR(150),
    ip_address VARCHAR(50),
    endpoint VARCHAR(100),
    action_status VARCHAR(50),
    can_run BOOLEAN NOT NULL DEFAULT FALSE,
    worker_id INT NULL DEFAULT 1,
    message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_access_logs_created ON access_logs(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_access_logs_key ON access_logs(access_key);
CREATE INDEX IF NOT EXISTS idx_access_logs_machine ON access_logs(machine_name);

-- ==============================================================================
-- FUNCIONES Y PROCEDIMIENTOS ALMACENADOS (POSTGRESQL / PLPGSQL)
-- ==============================================================================

-- 1. ACTIVACIÓN DE LICENCIA COMPLETA
CREATE OR REPLACE FUNCTION sp_activate_client_access(
    p_access_key VARCHAR,
    p_app_code VARCHAR DEFAULT 'att-bot'
)
RETURNS TABLE (
    access_id INT,
    client_name VARCHAR,
    access_key VARCHAR,
    app_code VARCHAR,
    access_status VARCHAR,
    last_payment_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ,
    days_left INT,
    grace_days INT,
    late_fee_per_day NUMERIC,
    manual_allow_until TIMESTAMPTZ,
    manual_allow_reason VARCHAR,
    overdue_days INT,
    real_access_status VARCHAR,
    late_fee_amount NUMERIC,
    is_manual_exception BOOLEAN,
    is_master BOOLEAN,
    can_run BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        c.access_id,
        c.client_name,
        c.access_key,
        c.app_code,
        c.access_status,
        c.last_payment_at,
        c.expires_at,

        CASE
            WHEN c.is_master THEN 99999
            WHEN CURRENT_DATE <= c.expires_at::DATE THEN (c.expires_at::DATE - CURRENT_DATE)::INT
            ELSE 0
        END AS days_left,

        c.grace_days,
        c.late_fee_per_day,
        c.manual_allow_until,
        c.manual_allow_reason,

        CASE
            WHEN c.is_master THEN 0
            WHEN CURRENT_DATE <= c.expires_at::DATE THEN 0
            ELSE (CURRENT_DATE - c.expires_at::DATE)::INT
        END AS overdue_days,

        (CASE
            WHEN c.is_master THEN 'ACTIVE'
            WHEN c.access_status = 'DISABLED' THEN 'BLOCKED'
            WHEN CURRENT_DATE <= c.expires_at::DATE THEN 'ACTIVE'
            WHEN c.manual_allow_until IS NOT NULL AND CURRENT_TIMESTAMP <= c.manual_allow_until THEN 'EXCEPTION'
            WHEN (CURRENT_DATE - c.expires_at::DATE)::INT <= c.grace_days THEN 'GRACE'
            ELSE 'BLOCKED'
        END)::VARCHAR AS real_access_status,

        CASE
            WHEN c.is_master THEN 0::NUMERIC
            WHEN CURRENT_DATE <= c.expires_at::DATE THEN 0::NUMERIC
            ELSE ((CURRENT_DATE - c.expires_at::DATE) * c.late_fee_per_day)::NUMERIC
        END AS late_fee_amount,

        CASE
            WHEN NOT c.is_master 
                 AND c.manual_allow_until IS NOT NULL 
                 AND CURRENT_TIMESTAMP <= c.manual_allow_until 
                 AND CURRENT_DATE > c.expires_at::DATE THEN TRUE
            ELSE FALSE
        END AS is_manual_exception,

        c.is_master,

        CASE
            WHEN c.is_master THEN TRUE
            WHEN c.access_status = 'DISABLED' THEN FALSE
            WHEN CURRENT_DATE <= c.expires_at::DATE THEN TRUE
            WHEN c.manual_allow_until IS NOT NULL AND CURRENT_TIMESTAMP <= c.manual_allow_until THEN TRUE
            WHEN (CURRENT_DATE - c.expires_at::DATE)::INT <= c.grace_days THEN TRUE
            ELSE FALSE
        END AS can_run
    FROM client_access_records c
    WHERE c.access_key = p_access_key
      AND c.app_code = p_app_code
    LIMIT 1;
END;
$$ LANGUAGE plpgsql;

-- 2. VERIFICACIÓN DIRECTA PARA BOTS ANTES DE OPERAR
CREATE OR REPLACE FUNCTION sp_verify_bot_access(
    p_access_key VARCHAR,
    p_app_code VARCHAR DEFAULT 'att-bot',
    p_worker_id INT DEFAULT 1,
    p_machine_name VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    can_run BOOLEAN,
    access_status VARCHAR,
    message VARCHAR,
    client_name VARCHAR,
    days_left INT,
    expires_at TIMESTAMPTZ,
    last_payment_at TIMESTAMPTZ,
    is_master BOOLEAN
) AS $$
DECLARE
    v_rec RECORD;
    v_days_left INT;
BEGIN
    SELECT * INTO v_rec
    FROM client_access_records
    WHERE access_key = p_access_key
      AND app_code = p_app_code;

    IF NOT FOUND THEN
        RETURN QUERY SELECT FALSE, 'INVALID'::VARCHAR, 'Licencia no encontrada o inválida'::VARCHAR, ''::VARCHAR, 0, NULL::TIMESTAMPTZ, NULL::TIMESTAMPTZ, FALSE;
        RETURN;
    END IF;

    IF v_rec.is_master THEN
        RETURN QUERY SELECT TRUE, 'ACTIVE'::VARCHAR, 'Licencia Maestra / Super Admin autorizada sin límites'::VARCHAR, v_rec.client_name, 99999, v_rec.expires_at, v_rec.last_payment_at, TRUE;
        RETURN;
    END IF;

    IF v_rec.access_status = 'DISABLED' THEN
        RETURN QUERY SELECT FALSE, 'BLOCKED'::VARCHAR, 'Licencia suspendida por administración'::VARCHAR, v_rec.client_name, 0, v_rec.expires_at, v_rec.last_payment_at, FALSE;
        RETURN;
    END IF;

    IF v_rec.manual_allow_until IS NOT NULL AND CURRENT_TIMESTAMP <= v_rec.manual_allow_until THEN
        v_days_left := GREATEST(0, (v_rec.expires_at::DATE - CURRENT_DATE)::INT);
        RETURN QUERY SELECT TRUE, 'EXCEPTION'::VARCHAR, ('Acceso bajo prórroga manual hasta ' || to_char(v_rec.manual_allow_until, 'YYYY-MM-DD HH24:MI'))::VARCHAR, v_rec.client_name, v_days_left, v_rec.expires_at, v_rec.last_payment_at, FALSE;
        RETURN;
    END IF;

    IF CURRENT_DATE <= v_rec.expires_at::DATE THEN
        v_days_left := (v_rec.expires_at::DATE - CURRENT_DATE)::INT;
        RETURN QUERY SELECT TRUE, 'ACTIVE'::VARCHAR, ('Licencia activa (' || v_days_left || ' días restantes)')::VARCHAR, v_rec.client_name, v_days_left, v_rec.expires_at, v_rec.last_payment_at, FALSE;
        RETURN;
    END IF;

    IF (CURRENT_DATE - v_rec.expires_at::DATE)::INT <= v_rec.grace_days THEN
        DECLARE
            v_grace_remaining INT := v_rec.grace_days - (CURRENT_DATE - v_rec.expires_at::DATE)::INT;
        BEGIN
            RETURN QUERY SELECT TRUE, 'GRACE'::VARCHAR, ('Licencia en período de gracia (' || v_grace_remaining || ' día(s) restante(s))')::VARCHAR, v_rec.client_name, 0, v_rec.expires_at, v_rec.last_payment_at, FALSE;
            RETURN;
        END;
    END IF;

    RETURN QUERY SELECT FALSE, 'BLOCKED'::VARCHAR, 'Licencia vencida. Renueva tus días con el administrador para continuar operando.'::VARCHAR, v_rec.client_name, 0, v_rec.expires_at, v_rec.last_payment_at, FALSE;
END;
$$ LANGUAGE plpgsql;

-- 3. SUMAR DÍAS EN 1 CLIC (ACTUALIZA ÚLTIMO PAGO)
CREATE OR REPLACE FUNCTION sp_add_license_days(
    p_access_key VARCHAR,
    p_days INT
)
RETURNS TABLE (
    success BOOLEAN,
    new_expires_at TIMESTAMPTZ,
    days_left INT,
    last_payment_at TIMESTAMPTZ,
    message VARCHAR
) AS $$
DECLARE
    v_current_exp TIMESTAMPTZ;
    v_new_exp TIMESTAMPTZ;
    v_now TIMESTAMPTZ := CURRENT_TIMESTAMP;
BEGIN
    SELECT expires_at INTO v_current_exp
    FROM client_access_records
    WHERE access_key = p_access_key;

    IF NOT FOUND THEN
        RETURN QUERY SELECT FALSE, NULL::TIMESTAMPTZ, 0, NULL::TIMESTAMPTZ, 'Licencia no encontrada'::VARCHAR;
        RETURN;
    END IF;

    IF v_current_exp < v_now THEN
        v_new_exp := v_now + (p_days || ' days')::INTERVAL;
    ELSE
        v_new_exp := v_current_exp + (p_days || ' days')::INTERVAL;
    END IF;

    UPDATE client_access_records
    SET expires_at = v_new_exp,
        access_status = 'ACTIVE',
        last_payment_at = v_now,
        updated_at = v_now
    WHERE access_key = p_access_key;

    RETURN QUERY SELECT 
        TRUE, 
        v_new_exp, 
        GREATEST(0, (v_new_exp::DATE - CURRENT_DATE)::INT),
        v_now,
        ('Se sumaron ' || p_days || ' días exitosamente. Último pago registrado: ' || to_char(v_now, 'YYYY-MM-DD HH24:MI'))::VARCHAR;
END;
$$ LANGUAGE plpgsql;

-- 4. CREAR NUEVA LICENCIA
CREATE OR REPLACE FUNCTION sp_create_client_access(
    p_client_name VARCHAR,
    p_app_code VARCHAR DEFAULT 'att-bot',
    p_prefix VARCHAR DEFAULT 'ATT',
    p_days_active INT DEFAULT 30,
    p_grace_days INT DEFAULT 3,
    p_late_fee_per_day NUMERIC DEFAULT 500.00
)
RETURNS TABLE (
    access_id INT,
    client_name VARCHAR,
    access_key VARCHAR,
    expires_at TIMESTAMPTZ,
    days_left INT,
    last_payment_at TIMESTAMPTZ
) AS $$
DECLARE
    v_new_key VARCHAR;
    v_inserted_id INT;
    v_now TIMESTAMPTZ := CURRENT_TIMESTAMP;
    v_exp TIMESTAMPTZ;
BEGIN
    v_new_key := UPPER(p_prefix) || '-' ||
                 UPPER(SUBSTRING(MD5(RANDOM()::TEXT), 1, 4)) || '-' ||
                 UPPER(SUBSTRING(MD5(RANDOM()::TEXT), 1, 4)) || '-' ||
                 UPPER(SUBSTRING(MD5(RANDOM()::TEXT), 1, 4));

    v_exp := v_now + (p_days_active || ' days')::INTERVAL;

    INSERT INTO client_access_records (
        client_name,
        access_key,
        app_code,
        access_status,
        is_master,
        last_payment_at,
        expires_at,
        grace_days,
        late_fee_per_day,
        created_at,
        updated_at
    ) VALUES (
        p_client_name,
        v_new_key,
        p_app_code,
        'ACTIVE',
        FALSE,
        v_now,
        v_exp,
        p_grace_days,
        p_late_fee_per_day,
        v_now,
        v_now
    )
    RETURNING client_access_records.access_id INTO v_inserted_id;

    RETURN QUERY SELECT 
        v_inserted_id, 
        p_client_name, 
        v_new_key, 
        v_exp, 
        p_days_active, 
        v_now;
END;
$$ LANGUAGE plpgsql;

-- 5. RESOLVER ACCESS_ID POR ACCESS_KEY
CREATE OR REPLACE FUNCTION sp_get_access_id_by_key(p_access_key VARCHAR)
RETURNS TABLE (id INT) AS $$
BEGIN
    RETURN QUERY
    SELECT access_id AS id FROM client_access_records WHERE access_key = p_access_key LIMIT 1;
END;
$$ LANGUAGE plpgsql;

-- Alias PascalCase
CREATE OR REPLACE FUNCTION "sp_GetAccessIdByKey"(p_access_key VARCHAR)
RETURNS TABLE (id INT) AS $$
BEGIN
    RETURN QUERY
    SELECT access_id AS id FROM client_access_records WHERE access_key = p_access_key LIMIT 1;
END;
$$ LANGUAGE plpgsql;

-- 6. CONFIGURACIÓN GLOBAL
CREATE OR REPLACE FUNCTION sp_get_global_config()
RETURNS TABLE (key VARCHAR, value TEXT, description VARCHAR) AS $$
BEGIN
    RETURN QUERY SELECT g.key, g.value, g.description FROM global_config g;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_GetGlobalConfig"()
RETURNS TABLE (key VARCHAR, value TEXT, description VARCHAR) AS $$
BEGIN
    RETURN QUERY SELECT g.key, g.value, g.description FROM global_config g;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_save_global_config(p_key VARCHAR, p_value TEXT)
RETURNS VOID AS $$
BEGIN
    INSERT INTO global_config (key, value, updated_at)
    VALUES (p_key, p_value, CURRENT_TIMESTAMP)
    ON CONFLICT (key) DO UPDATE
    SET value = p_value, updated_at = CURRENT_TIMESTAMP;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_SaveGlobalConfig"(p_key VARCHAR, p_value TEXT)
RETURNS VOID AS $$
BEGIN
    PERFORM sp_save_global_config(p_key, p_value);
END;
$$ LANGUAGE plpgsql;

-- 7. SECUENCIAS
CREATE OR REPLACE FUNCTION sp_create_sequence(
    p_access_id INT,
    p_sequence VARCHAR,
    p_state_code VARCHAR DEFAULT NULL,
    p_state_name VARCHAR DEFAULT NULL
)
RETURNS TABLE (id INT) AS $$
DECLARE
    v_existing_id INT;
    v_new_id INT;
BEGIN
    SELECT s.id INTO v_existing_id 
    FROM sequences s 
    WHERE s.access_id = p_access_id AND s.sequence = p_sequence 
    LIMIT 1;

    IF v_existing_id IS NOT NULL THEN
        RETURN QUERY SELECT v_existing_id;
    ELSE
        INSERT INTO sequences (access_id, sequence, state_code, state_name)
        VALUES (p_access_id, p_sequence, p_state_code, p_state_name)
        RETURNING sequences.id INTO v_new_id;
        RETURN QUERY SELECT v_new_id;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_CreateSequence"(
    p_access_id INT,
    p_sequence VARCHAR,
    p_state_code VARCHAR DEFAULT NULL,
    p_state_name VARCHAR DEFAULT NULL
)
RETURNS TABLE (id INT) AS $$
BEGIN
    RETURN QUERY SELECT * FROM sp_create_sequence(p_access_id, p_sequence, p_state_code, p_state_name);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_get_sequences_by_access(p_access_id INT)
RETURNS TABLE (
    id INT,
    sequence VARCHAR,
    state_code VARCHAR,
    state_name VARCHAR,
    total_leads INT,
    status VARCHAR,
    created_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY
    SELECT s.id, s.sequence, s.state_code, s.state_name, s.total_leads, s.status, s.created_at, s.completed_at
    FROM sequences s
    WHERE s.access_id = p_access_id
    ORDER BY s.created_at DESC;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_GetSequencesByAccess"(p_access_id INT)
RETURNS TABLE (
    id INT,
    sequence VARCHAR,
    state_code VARCHAR,
    state_name VARCHAR,
    total_leads INT,
    status VARCHAR,
    created_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY SELECT * FROM sp_get_sequences_by_access(p_access_id);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_claim_sequence(p_access_id INT)
RETURNS TABLE (
    id INT,
    sequence VARCHAR,
    state_code VARCHAR,
    state_name VARCHAR
) AS $$
DECLARE
    v_seq_id INT;
BEGIN
    SELECT s.id INTO v_seq_id
    FROM sequences s
    WHERE s.access_id = p_access_id AND s.status = 'pending'
    ORDER BY s.id ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED;

    IF v_seq_id IS NOT NULL THEN
        UPDATE sequences SET status = 'processing' WHERE sequences.id = v_seq_id;
        RETURN QUERY SELECT s.id, s.sequence, s.state_code, s.state_name FROM sequences s WHERE s.id = v_seq_id;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_ClaimSequence"(p_access_id INT)
RETURNS TABLE (
    id INT,
    sequence VARCHAR,
    state_code VARCHAR,
    state_name VARCHAR
) AS $$
BEGIN
    RETURN QUERY SELECT * FROM sp_claim_sequence(p_access_id);
END;
$$ LANGUAGE plpgsql;

-- 8. LEADS
CREATE OR REPLACE FUNCTION sp_insert_lead(
    p_access_id INT,
    p_sequence_id INT,
    p_phone_number VARCHAR,
    p_full_name VARCHAR DEFAULT NULL,
    p_address VARCHAR DEFAULT NULL,
    p_zip_code VARCHAR DEFAULT NULL,
    p_state_code VARCHAR DEFAULT NULL,
    p_source VARCHAR DEFAULT 'generator',
    p_status VARCHAR DEFAULT 'pending'
)
RETURNS VOID AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM leads WHERE access_id = p_access_id AND phone_number = p_phone_number) THEN
        INSERT INTO leads (access_id, sequence_id, phone_number, full_name, address, zip_code, state_code, source, status)
        VALUES (p_access_id, p_sequence_id, p_phone_number, p_full_name, p_address, p_zip_code, p_state_code, p_source, p_status);
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_InsertLead"(
    p_access_id INT,
    p_sequence_id INT,
    p_phone_number VARCHAR,
    p_full_name VARCHAR DEFAULT NULL,
    p_address VARCHAR DEFAULT NULL,
    p_zip_code VARCHAR DEFAULT NULL,
    p_state_code VARCHAR DEFAULT NULL,
    p_source VARCHAR DEFAULT 'generator',
    p_status VARCHAR DEFAULT 'pending'
)
RETURNS VOID AS $$
BEGIN
    PERFORM sp_insert_lead(p_access_id, p_sequence_id, p_phone_number, p_full_name, p_address, p_zip_code, p_state_code, p_source, p_status);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_get_pending_leads(p_access_id INT, p_limit INT DEFAULT 100)
RETURNS TABLE (
    id INT,
    phone_number VARCHAR,
    full_name VARCHAR,
    address VARCHAR,
    zip_code VARCHAR,
    state_code VARCHAR
) AS $$
BEGIN
    RETURN QUERY
    SELECT l.id, l.phone_number, l.full_name, l.address, l.zip_code, l.state_code
    FROM leads l
    WHERE l.access_id = p_access_id AND l.status = 'pending'
    ORDER BY l.id ASC
    LIMIT p_limit;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_GetPendingLeads"(p_access_id INT, p_limit INT DEFAULT 100)
RETURNS TABLE (
    id INT,
    phone_number VARCHAR,
    full_name VARCHAR,
    address VARCHAR,
    zip_code VARCHAR,
    state_code VARCHAR
) AS $$
BEGIN
    RETURN QUERY SELECT * FROM sp_get_pending_leads(p_access_id, p_limit);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_get_next_lead(p_access_id INT)
RETURNS TABLE (
    lead_id INT,
    phone_number VARCHAR,
    full_name VARCHAR,
    address VARCHAR,
    zip_code VARCHAR,
    state_code VARCHAR
) AS $$
DECLARE
    v_lead_id INT;
BEGIN
    SELECT l.id INTO v_lead_id
    FROM leads l
    WHERE l.access_id = p_access_id AND l.status = 'pending'
    ORDER BY l.id ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED;

    IF v_lead_id IS NOT NULL THEN
        UPDATE leads SET status = 'processing', processed_at = CURRENT_TIMESTAMP WHERE id = v_lead_id;
        RETURN QUERY SELECT l.id, l.phone_number, l.full_name, l.address, l.zip_code, l.state_code FROM leads l WHERE l.id = v_lead_id;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_GetNextLead"(p_access_id INT)
RETURNS TABLE (
    lead_id INT,
    phone_number VARCHAR,
    full_name VARCHAR,
    address VARCHAR,
    zip_code VARCHAR,
    state_code VARCHAR
) AS $$
BEGIN
    RETURN QUERY SELECT * FROM sp_get_next_lead(p_access_id);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_update_lead_status(p_lead_id INT, p_status VARCHAR, p_processed_at TIMESTAMPTZ DEFAULT NULL)
RETURNS VOID AS $$
BEGIN
    UPDATE leads SET status = p_status, processed_at = COALESCE(p_processed_at, CURRENT_TIMESTAMP) WHERE id = p_lead_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_UpdateLeadStatus"(p_lead_id INT, p_status VARCHAR, p_processed_at TIMESTAMPTZ DEFAULT NULL)
RETURNS VOID AS $$
BEGIN
    PERFORM sp_update_lead_status(p_lead_id, p_status, p_processed_at);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_count_leads(p_access_id INT, p_status VARCHAR DEFAULT NULL)
RETURNS TABLE (count BIGINT) AS $$
BEGIN
    IF p_status IS NULL THEN
        RETURN QUERY SELECT COUNT(*) FROM leads WHERE access_id = p_access_id;
    ELSE
        RETURN QUERY SELECT COUNT(*) FROM leads WHERE access_id = p_access_id AND status = p_status;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_CountLeads"(p_access_id INT, p_status VARCHAR DEFAULT NULL)
RETURNS TABLE (count BIGINT) AS $$
BEGIN
    RETURN QUERY SELECT * FROM sp_count_leads(p_access_id, p_status);
END;
$$ LANGUAGE plpgsql;

-- 9. HITS
CREATE OR REPLACE FUNCTION sp_insert_hit(
    p_access_id INT,
    p_lead_id INT,
    p_phone_number VARCHAR,
    p_full_name VARCHAR DEFAULT NULL,
    p_address VARCHAR DEFAULT NULL,
    p_zip_code VARCHAR DEFAULT NULL,
    p_device_message VARCHAR DEFAULT NULL,
    p_device_type VARCHAR DEFAULT NULL,
    p_hit_type VARCHAR DEFAULT NULL,
    p_profile_raw TEXT DEFAULT NULL
)
RETURNS TABLE (id INT) AS $$
DECLARE
    v_hit_id INT;
BEGIN
    INSERT INTO hits (
        access_id, lead_id, phone_number, full_name, address, zip_code,
        device_message, device_type, hit_type, profile_raw, created_at
    ) VALUES (
        p_access_id, p_lead_id, p_phone_number, p_full_name, p_address, p_zip_code,
        p_device_message, p_device_type, p_hit_type, p_profile_raw, CURRENT_TIMESTAMP
    )
    RETURNING hits.id INTO v_hit_id;

    IF p_lead_id IS NOT NULL THEN
        UPDATE leads SET status = 'hit', processed_at = CURRENT_TIMESTAMP WHERE leads.id = p_lead_id;
    END IF;

    RETURN QUERY SELECT v_hit_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_InsertHit"(
    p_access_id INT,
    p_lead_id INT,
    p_phone_number VARCHAR,
    p_full_name VARCHAR DEFAULT NULL,
    p_address VARCHAR DEFAULT NULL,
    p_zip_code VARCHAR DEFAULT NULL,
    p_device_message VARCHAR DEFAULT NULL,
    p_device_type VARCHAR DEFAULT NULL,
    p_hit_type VARCHAR DEFAULT NULL,
    p_profile_raw TEXT DEFAULT NULL
)
RETURNS TABLE (id INT) AS $$
BEGIN
    RETURN QUERY SELECT * FROM sp_insert_hit(
        p_access_id, p_lead_id, p_phone_number, p_full_name, p_address, p_zip_code,
        p_device_message, p_device_type, p_hit_type, p_profile_raw
    );
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_get_hits_by_access(p_access_id INT)
RETURNS TABLE (
    id INT,
    phone_number VARCHAR,
    full_name VARCHAR,
    address VARCHAR,
    zip_code VARCHAR,
    device_message VARCHAR,
    device_type VARCHAR,
    hit_type VARCHAR,
    created_at TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY
    SELECT h.id, h.phone_number, h.full_name, h.address, h.zip_code, h.device_message, h.device_type, h.hit_type, h.created_at
    FROM hits h
    WHERE h.access_id = p_access_id
    ORDER BY h.created_at DESC;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_GetHitsByAccess"(p_access_id INT)
RETURNS TABLE (
    id INT,
    phone_number VARCHAR,
    full_name VARCHAR,
    address VARCHAR,
    zip_code VARCHAR,
    device_message VARCHAR,
    device_type VARCHAR,
    hit_type VARCHAR,
    created_at TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY SELECT * FROM sp_get_hits_by_access(p_access_id);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_get_dashboard_stats(p_access_id INT)
RETURNS TABLE (
    total_leads BIGINT,
    pending_leads BIGINT,
    hits BIGINT,
    total_sequences BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        (SELECT COUNT(*) FROM leads WHERE access_id = p_access_id)::BIGINT AS total_leads,
        (SELECT COUNT(*) FROM leads WHERE access_id = p_access_id AND status = 'pending')::BIGINT AS pending_leads,
        (SELECT COUNT(*) FROM hits WHERE access_id = p_access_id)::BIGINT AS hits,
        (SELECT COUNT(*) FROM sequences WHERE access_id = p_access_id)::BIGINT AS total_sequences;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION "sp_GetDashboardStats"(p_access_id INT)
RETURNS TABLE (
    total_leads BIGINT,
    pending_leads BIGINT,
    hits BIGINT,
    total_sequences BIGINT
) AS $$
BEGIN
    RETURN QUERY SELECT * FROM sp_get_dashboard_stats(p_access_id);
END;
$$ LANGUAGE plpgsql;

-- ==============================================================================
-- SEMILLAS INICIALES OBLIGATORIAS (SUPER ADMIN & CONFIGS)
-- ==============================================================================

-- 1. Licencia Maestra / Super Admin (Root permanente sin vencimiento)
INSERT INTO client_access_records (
    client_name,
    access_key,
    app_code,
    access_status,
    is_master,
    last_payment_at,
    expires_at,
    grace_days,
    late_fee_per_day
) VALUES (
    'Administrador Principal (Super Admin)',
    'ATT-MASTER-ADMIN-2026',
    'att-bot',
    'ACTIVE',
    TRUE,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP + INTERVAL '3650 days',
    999,
    0.00
) ON CONFLICT (access_key) DO UPDATE
SET is_master = TRUE, access_status = 'ACTIVE', last_payment_at = CURRENT_TIMESTAMP;

-- 2. Licencia Demo de Prueba (30 días)
INSERT INTO client_access_records (
    client_name,
    access_key,
    app_code,
    access_status,
    is_master,
    last_payment_at,
    expires_at,
    grace_days,
    late_fee_per_day
) VALUES (
    'Cliente Demo',
    'ATT-DEMO-2026-TEST',
    'att-bot',
    'ACTIVE',
    FALSE,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP + INTERVAL '30 days',
    3,
    500.00
) ON CONFLICT (access_key) DO NOTHING;

-- 3. Licencia Expirada de Prueba (Vencida hace 15 días para pruebas de bloqueo y mora)
INSERT INTO client_access_records (
    client_name,
    access_key,
    app_code,
    access_status,
    is_master,
    last_payment_at,
    expires_at,
    grace_days,
    late_fee_per_day
) VALUES (
    'Cliente Expirado (Pruebas)',
    'ATT-EXPIRED-TEST-2026',
    'att-bot',
    'ACTIVE',
    FALSE,
    CURRENT_TIMESTAMP - INTERVAL '45 days',
    CURRENT_TIMESTAMP - INTERVAL '15 days',
    3,
    500.00
) ON CONFLICT (access_key) DO UPDATE
SET expires_at = CURRENT_TIMESTAMP - INTERVAL '15 days', is_master = FALSE;

-- 4. Valores por defecto de Configuración Global
INSERT INTO global_config (key, value, description) VALUES
    ('KEYABS', '', 'GoLogin API Key'),
    ('KEY', '', 'Cryptolens Key / Access Key'),
    ('PROXY_TYPE_2', 'http', 'Proxy type for generator'),
    ('PROXY_SERVER_2', '', 'Proxy server for generator'),
    ('PROXY_PORT_2', '', 'Proxy port for generator'),
    ('PROXY_USER_2', '', 'Proxy user for generator'),
    ('PROXY_PASS_2', '', 'Proxy password for generator'),
    ('TELEGRAM_BOT_TOKEN', '', 'Telegram Bot Token'),
    ('TELEGRAM_CHAT_ID', '', 'Telegram Chat ID')
ON CONFLICT (key) DO NOTHING;
