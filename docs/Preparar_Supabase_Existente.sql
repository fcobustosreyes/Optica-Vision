-- Preparacion incremental para el proyecto Supabase ya existente.
-- Revise y respalde la base antes de ejecutarlo. No elimina ni recrea datos.
-- Ejecutar una sola vez en el SQL Editor del proyecto amyjnztkqlxobpbreslj.
BEGIN;

ALTER TABLE optica.roles
    ADD COLUMN IF NOT EXISTS activo boolean NOT NULL DEFAULT true;

ALTER TABLE optica.perfiles
    ADD COLUMN IF NOT EXISTS auth_user_id uuid;

ALTER TABLE optica.perfiles
    ADD COLUMN IF NOT EXISTS correo_login varchar(254);

ALTER TABLE optica.perfiles
    ADD COLUMN IF NOT EXISTS debe_cambiar_contrasena boolean NOT NULL DEFAULT false;

CREATE UNIQUE INDEX IF NOT EXISTS ux_perfiles_auth_user_id
    ON optica.perfiles(auth_user_id)
    WHERE auth_user_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_perfiles_correo_login
    ON optica.perfiles(lower(correo_login))
    WHERE correo_login IS NOT NULL;

INSERT INTO optica.roles(nombre, descripcion, activo) VALUES
    ('superadministrador', 'Administra la plataforma y el acceso de soporte, con auditoria.', true),
    ('administrador', 'Administra usuarios y operaciones de su optica.', true),
    ('ventas', 'Registra ventas y consulta la informacion necesaria para atender clientes.', true),
    ('recepcion', 'Gestiona citas y consulta datos minimos de contacto.', true),
    ('cliente', 'Consulta y administra unicamente su perfil y sus citas.', true)
ON CONFLICT (nombre) DO UPDATE
SET descripcion = EXCLUDED.descripcion,
    activo = true;

-- Conserva los perfiles asignados al rol vendedor y los transfiere a ventas.
UPDATE optica.perfiles AS p
SET rol_id = (SELECT id FROM optica.roles WHERE nombre = 'ventas')
WHERE p.rol_id = (SELECT id FROM optica.roles WHERE nombre = 'vendedor')
  AND EXISTS (SELECT 1 FROM optica.roles WHERE nombre = 'ventas' AND activo = true);

-- Mantiene los roles antiguos para el historial, pero evita asignarlos de nuevo.
UPDATE optica.roles
SET activo = false
WHERE nombre IN ('vendedor', 'bodeguero');

CREATE TABLE IF NOT EXISTS optica.auditoria_eventos (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    actor_perfil_id uuid REFERENCES optica.perfiles(id),
    tipo varchar(60) NOT NULL,
    entidad varchar(80) NOT NULL,
    entidad_id bigint,
    detalles jsonb NOT NULL DEFAULT '{}'::jsonb,
    registrado_en timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_auditoria_eventos_entidad_fecha
    ON optica.auditoria_eventos(entidad, entidad_id, registrado_en);

COMMENT ON COLUMN optica.perfiles.auth_user_id IS
    'UUID de la cuenta autenticada en Supabase Auth; no contiene contrasenas.';
COMMENT ON COLUMN optica.perfiles.correo_login IS
    'Correo usado para iniciar sesion en Supabase Auth.';
COMMENT ON COLUMN optica.perfiles.debe_cambiar_contrasena IS
    'Exige establecer una contrasena propia antes de usar el sistema.';
COMMENT ON TABLE optica.auditoria_eventos IS
    'Bitacora de operaciones; no almacenar contrasenas, tokens ni valores personales innecesarios.';

COMMIT;
