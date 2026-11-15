-- Sincroniza el esquema existente con el flujo de cuentas de la aplicacion.
-- Revisar en Supabase antes de ejecutarlo en produccion.
BEGIN;

ALTER TABLE optica.roles
    ADD COLUMN IF NOT EXISTS activo boolean NOT NULL DEFAULT true;

ALTER TABLE optica.perfiles
    ADD COLUMN IF NOT EXISTS auth_user_id uuid,
    ADD COLUMN IF NOT EXISTS correo_login varchar(254),
    ADD COLUMN IF NOT EXISTS debe_cambiar_contrasena boolean NOT NULL DEFAULT false;

CREATE TABLE IF NOT EXISTS optica.auditoria_eventos (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    actor_perfil_id uuid REFERENCES optica.perfiles(id),
    tipo varchar(60) NOT NULL,
    entidad varchar(80) NOT NULL,
    entidad_id bigint,
    detalles jsonb NOT NULL DEFAULT '{}'::jsonb,
    registrado_en timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS auditoria_eventos_entidad_fecha_idx
    ON optica.auditoria_eventos(entidad, entidad_id, registrado_en);

CREATE UNIQUE INDEX IF NOT EXISTS perfiles_auth_user_id_unico
    ON optica.perfiles(auth_user_id)
    WHERE auth_user_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS perfiles_correo_login_unico
    ON optica.perfiles(lower(correo_login))
    WHERE correo_login IS NOT NULL;

-- La base actual usa "vendedor" y "bodeguero"; la aplicacion usa nombres
-- canonicos para autorizar los menus. Si ambos nombres ya existen, mueve los
-- perfiles al rol canonico antes de dejar intacto el alias antiguo.
DO $roles$
DECLARE
    par record;
    id_canonico bigint;
    id_alias bigint;
BEGIN
    FOR par IN
        SELECT * FROM (VALUES ('vendedor', 'ventas'), ('bodeguero', 'bodega')) AS nombres(alias, canonico)
    LOOP
        SELECT id INTO id_alias FROM optica.roles WHERE nombre = par.alias;
        SELECT id INTO id_canonico FROM optica.roles WHERE nombre = par.canonico;
        IF id_alias IS NOT NULL AND id_canonico IS NULL THEN
            UPDATE optica.roles SET nombre = par.canonico WHERE id = id_alias;
        ELSIF id_alias IS NOT NULL AND id_canonico IS NOT NULL THEN
            UPDATE optica.perfiles SET rol_id = id_canonico WHERE rol_id = id_alias;
        END IF;
    END LOOP;
END
$roles$;

INSERT INTO optica.roles(nombre, descripcion) VALUES
    ('superadministrador', 'Administra las cuentas y los permisos del sistema.'),
    ('ventas', 'Gestiona ventas, clientes y consulta de existencias.'),
    ('recepcion', 'Gestiona citas y consulta los datos necesarios de clientes.'),
    ('bodega', 'Gestiona recepciones, existencias y el kardex de inventario.')
ON CONFLICT (nombre) DO NOTHING;

COMMIT;
