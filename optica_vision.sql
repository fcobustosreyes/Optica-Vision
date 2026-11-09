-- Optica Vision | PostgreSQL 16+ | Instalacion inicial, ejecutar una sola vez.
-- Esquema independiente para uso desde el servidor ASP.NET Core.
-- No crea usuarios, contrasenas ni politicas de acceso desde el navegador.
BEGIN;
CREATE SCHEMA optica;
REVOKE ALL ON SCHEMA optica FROM PUBLIC;
SET LOCAL search_path = optica, pg_catalog;

CREATE TABLE roles (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 nombre varchar(40) NOT NULL UNIQUE, descripcion text
);
CREATE TABLE perfiles (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 rol_id bigint NOT NULL REFERENCES roles,
 nombre_completo varchar(150) NOT NULL,
 activo boolean NOT NULL DEFAULT true,
 creado_en timestamptz NOT NULL DEFAULT now()
);
-- perfiles.id puede vincularse al proveedor de autenticacion elegido.
-- No almacene contrasenas en perfiles.
CREATE TABLE clientes (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 codigo varchar(30) NOT NULL UNIQUE,
 nombres varchar(100) NOT NULL, apellidos varchar(100) NOT NULL,
 telefono varchar(30), correo varchar(254), direccion text,
 activo boolean NOT NULL DEFAULT true, creado_en timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE proveedores (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 nombre varchar(150) NOT NULL, identificacion_fiscal varchar(40) UNIQUE,
 contacto varchar(150), telefono varchar(30), correo varchar(254), direccion text,
 activo boolean NOT NULL DEFAULT true
);
CREATE TABLE categorias (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 nombre varchar(80) NOT NULL UNIQUE, descripcion text,
 activo boolean NOT NULL DEFAULT true
);
CREATE TABLE marcas (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 nombre varchar(80) NOT NULL UNIQUE, activo boolean NOT NULL DEFAULT true
);
CREATE TABLE productos (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 codigo varchar(50) NOT NULL UNIQUE, nombre varchar(150) NOT NULL,
 categoria_id bigint NOT NULL REFERENCES categorias,
 marca_id bigint REFERENCES marcas,
 modelo varchar(80), color varchar(50), descripcion text,
 precio_venta numeric(14,2) NOT NULL CHECK (precio_venta >= 0),
 stock_minimo integer NOT NULL DEFAULT 0 CHECK (stock_minimo >= 0),
 activo boolean NOT NULL DEFAULT true, creado_en timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE bodegas (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 nombre varchar(80) NOT NULL UNIQUE, ubicacion text,
 activo boolean NOT NULL DEFAULT true
);
CREATE TABLE compras (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 proveedor_id bigint NOT NULL REFERENCES proveedores,
 numero_documento_proveedor varchar(80),
 fecha timestamptz NOT NULL DEFAULT now(),
 estado varchar(20) NOT NULL DEFAULT 'borrador'
 CHECK (estado IN ('borrador','confirmada','anulada')),
 creado_por uuid NOT NULL REFERENCES perfiles, observaciones text,
 UNIQUE (proveedor_id, numero_documento_proveedor)
);
CREATE TABLE detalle_compras (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 compra_id bigint NOT NULL REFERENCES compras,
 producto_id bigint NOT NULL REFERENCES productos,
 cantidad integer NOT NULL CHECK (cantidad > 0),
 costo_unitario numeric(14,2) NOT NULL CHECK (costo_unitario >= 0),
 subtotal numeric(24,2) GENERATED ALWAYS AS (cantidad * costo_unitario) STORED,
 UNIQUE (compra_id, producto_id)
);
CREATE TABLE recepciones (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 compra_id bigint NOT NULL REFERENCES compras,
 bodega_id bigint NOT NULL REFERENCES bodegas,
 fecha timestamptz NOT NULL DEFAULT now(),
 estado varchar(20) NOT NULL DEFAULT 'borrador'
 CHECK (estado IN ('borrador','confirmada','anulada')),
 recibido_por uuid NOT NULL REFERENCES perfiles, observaciones text
);
CREATE TABLE detalle_recepciones (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 recepcion_id bigint NOT NULL REFERENCES recepciones,
 detalle_compra_id bigint NOT NULL REFERENCES detalle_compras,
 cantidad_recibida integer NOT NULL CHECK (cantidad_recibida > 0),
 UNIQUE (recepcion_id, detalle_compra_id)
);
CREATE TABLE existencias (
 bodega_id bigint NOT NULL REFERENCES bodegas,
 producto_id bigint NOT NULL REFERENCES productos,
 cantidad bigint NOT NULL DEFAULT 0 CHECK (cantidad >= 0),
 PRIMARY KEY (bodega_id, producto_id)
);
CREATE TABLE ventas (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 numero bigint GENERATED ALWAYS AS IDENTITY UNIQUE,
 cliente_id bigint REFERENCES clientes,
 bodega_id bigint NOT NULL REFERENCES bodegas,
 fecha timestamptz NOT NULL DEFAULT now(),
 estado varchar(20) NOT NULL DEFAULT 'borrador'
 CHECK (estado IN ('borrador','confirmada','anulada')),
 vendedor_id uuid NOT NULL REFERENCES perfiles
);
CREATE TABLE detalle_ventas (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 venta_id bigint NOT NULL REFERENCES ventas,
 producto_id bigint NOT NULL REFERENCES productos,
 cantidad integer NOT NULL CHECK (cantidad > 0),
 precio_unitario numeric(14,2) NOT NULL CHECK (precio_unitario >= 0),
 descuento_unitario numeric(14,2) NOT NULL DEFAULT 0
 CHECK (descuento_unitario >= 0 AND descuento_unitario <= precio_unitario),
 subtotal numeric(24,2) GENERATED ALWAYS AS
 (cantidad * (precio_unitario - descuento_unitario)) STORED,
 UNIQUE (venta_id, producto_id)
);
CREATE TABLE pagos_venta (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 venta_id bigint NOT NULL REFERENCES ventas,
 metodo varchar(20) NOT NULL CHECK (metodo IN ('efectivo','tarjeta','transferencia')),
 monto numeric(14,2) NOT NULL CHECK (monto > 0),
 fecha timestamptz NOT NULL DEFAULT now(), referencia varchar(150)
);
CREATE TABLE conteos_inventario (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 bodega_id bigint NOT NULL REFERENCES bodegas,
 fecha timestamptz NOT NULL DEFAULT now(),
 estado varchar(20) NOT NULL DEFAULT 'abierto'
 CHECK (estado IN ('abierto','aprobado','cancelado')),
 creado_por uuid NOT NULL REFERENCES perfiles,
 aprobado_por uuid REFERENCES perfiles, observaciones text,
 CHECK (estado <> 'aprobado' OR aprobado_por IS NOT NULL)
);
CREATE UNIQUE INDEX un_conteo_abierto_por_bodega
 ON conteos_inventario (bodega_id) WHERE estado = 'abierto';
CREATE TABLE detalle_conteos (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 conteo_id bigint NOT NULL REFERENCES conteos_inventario,
 producto_id bigint NOT NULL REFERENCES productos,
 cantidad_sistema bigint NOT NULL CHECK (cantidad_sistema >= 0),
 cantidad_contada bigint CHECK (cantidad_contada >= 0),
 diferencia bigint GENERATED ALWAYS AS (cantidad_contada - cantidad_sistema) STORED,
 motivo text,
 UNIQUE (conteo_id, producto_id)
);
CREATE TABLE movimientos_inventario (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 bodega_id bigint NOT NULL REFERENCES bodegas,
 producto_id bigint NOT NULL REFERENCES productos,
 tipo varchar(30) NOT NULL CHECK (tipo IN
 ('recepcion','venta','ajuste','anulacion_recepcion','anulacion_venta')),
 cantidad bigint NOT NULL CHECK (cantidad <> 0),
 fecha timestamptz NOT NULL DEFAULT now(),
 usuario_id uuid NOT NULL REFERENCES perfiles,
 detalle_recepcion_id bigint REFERENCES detalle_recepciones,
 detalle_venta_id bigint REFERENCES detalle_ventas,
 detalle_conteo_id bigint REFERENCES detalle_conteos,
 CHECK (
 (tipo='recepcion' AND cantidad>0 AND detalle_recepcion_id IS NOT NULL
   AND detalle_venta_id IS NULL AND detalle_conteo_id IS NULL) OR
 (tipo='anulacion_recepcion' AND cantidad<0 AND detalle_recepcion_id IS NOT NULL
   AND detalle_venta_id IS NULL AND detalle_conteo_id IS NULL) OR
 (tipo='venta' AND cantidad<0 AND detalle_venta_id IS NOT NULL
   AND detalle_recepcion_id IS NULL AND detalle_conteo_id IS NULL) OR
 (tipo='anulacion_venta' AND cantidad>0 AND detalle_venta_id IS NOT NULL
   AND detalle_recepcion_id IS NULL AND detalle_conteo_id IS NULL) OR
 (tipo='ajuste' AND detalle_conteo_id IS NOT NULL
   AND detalle_recepcion_id IS NULL AND detalle_venta_id IS NULL)),
 UNIQUE (tipo, detalle_recepcion_id), UNIQUE (tipo, detalle_venta_id),
 UNIQUE (detalle_conteo_id)
);

-- Los detalles se editan solo mientras su documento esta en preparacion.
-- Bloquear la cabecera evita confirmar mientras otro usuario modifica sus lineas.
CREATE FUNCTION proteger_detalle() RETURNS trigger LANGUAGE plpgsql
SET search_path = optica, pg_catalog AS $$
DECLARE doc bigint; anterior bigint; estado_actual text; tabla text; campo text;
BEGIN
 tabla := TG_ARGV[0]; campo := TG_ARGV[1];
 IF TG_OP='DELETE' THEN doc := (to_jsonb(OLD)->>campo)::bigint;
 ELSE doc := (to_jsonb(NEW)->>campo)::bigint; END IF;
 IF TG_OP='UPDATE' THEN
   anterior := (to_jsonb(OLD)->>campo)::bigint;
   IF anterior <> doc THEN RAISE EXCEPTION 'No se puede cambiar el documento de un detalle'; END IF;
 END IF;
 EXECUTE format('SELECT estado FROM optica.%I WHERE id=$1 FOR UPDATE',tabla)
 INTO estado_actual USING doc;
 IF estado_actual IS NULL OR estado_actual NOT IN ('borrador','abierto') THEN
   RAISE EXCEPTION 'El documento no permite modificar detalles';
 END IF;
 IF TG_TABLE_NAME='detalle_conteos' THEN
   IF TG_OP='DELETE' THEN RAISE EXCEPTION 'No elimine lineas del corte; cancele el conteo'; END IF;
   IF TG_OP='UPDATE' THEN
     IF NEW.producto_id <> OLD.producto_id OR NEW.cantidad_sistema <> OLD.cantidad_sistema THEN
       RAISE EXCEPTION 'No se puede modificar el corte del inventario';
     END IF;
   ELSE
     SELECT COALESCE((SELECT e.cantidad FROM existencias e
     JOIN conteos_inventario c ON c.bodega_id=e.bodega_id
     WHERE c.id=doc AND e.producto_id=NEW.producto_id),0) INTO NEW.cantidad_sistema;
   END IF;
 END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER proteger BEFORE INSERT OR UPDATE OR DELETE ON detalle_compras
 FOR EACH ROW EXECUTE FUNCTION proteger_detalle('compras','compra_id');
CREATE TRIGGER proteger BEFORE INSERT OR UPDATE OR DELETE ON detalle_recepciones
 FOR EACH ROW EXECUTE FUNCTION proteger_detalle('recepciones','recepcion_id');
CREATE TRIGGER proteger BEFORE INSERT OR UPDATE OR DELETE ON detalle_ventas
 FOR EACH ROW EXECUTE FUNCTION proteger_detalle('ventas','venta_id');
CREATE TRIGGER proteger BEFORE INSERT OR UPDATE OR DELETE ON pagos_venta
 FOR EACH ROW EXECUTE FUNCTION proteger_detalle('ventas','venta_id');
CREATE TRIGGER proteger BEFORE INSERT OR UPDATE OR DELETE ON detalle_conteos
 FOR EACH ROW EXECUTE FUNCTION proteger_detalle('conteos_inventario','conteo_id');

CREATE FUNCTION aplicar_movimiento() RETURNS trigger LANGUAGE plpgsql
SET search_path = optica, pg_catalog AS $$
BEGIN
 IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Los movimientos son inmutables'; END IF;
 -- Solo los triggers de documentos pueden crear movimientos.
 IF pg_trigger_depth() < 2 THEN RAISE EXCEPTION 'Cambie el estado del documento para mover inventario'; END IF;
 PERFORM 1 FROM bodegas WHERE id=NEW.bodega_id FOR UPDATE;
 IF EXISTS (SELECT 1 FROM conteos_inventario c WHERE c.bodega_id=NEW.bodega_id
 AND c.estado='abierto' AND NOT EXISTS
 (SELECT 1 FROM detalle_conteos d WHERE d.id=NEW.detalle_conteo_id AND d.conteo_id=c.id)) THEN
   RAISE EXCEPTION 'Bodega en conteo: finalicelo antes de mover inventario';
 END IF;
 INSERT INTO existencias(bodega_id,producto_id,cantidad)
 VALUES(NEW.bodega_id,NEW.producto_id,0) ON CONFLICT DO NOTHING;
 UPDATE existencias SET cantidad=cantidad+NEW.cantidad
 WHERE bodega_id=NEW.bodega_id AND producto_id=NEW.producto_id;
 -- CHECK cantidad >= 0 aborta toda la transaccion si no alcanza el stock.
 RETURN NEW;
END $$;
CREATE TRIGGER aplicar BEFORE INSERT OR UPDATE OR DELETE ON movimientos_inventario
 FOR EACH ROW EXECUTE FUNCTION aplicar_movimiento();

CREATE FUNCTION proteger_existencias() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF pg_trigger_depth() < 2 THEN RAISE EXCEPTION 'Las existencias se actualizan mediante movimientos'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER proteger BEFORE INSERT OR UPDATE OR DELETE ON existencias
 FOR EACH ROW EXECUTE FUNCTION proteger_existencias();

CREATE FUNCTION procesar_documento() RETURNS trigger LANGUAGE plpgsql
SET search_path = optica, pg_catalog AS $$
DECLARE r record; total numeric; pagado numeric; recibida bigint; actor uuid;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Conserve el documento; use su estado de anulacion'; END IF;
 IF TG_OP='INSERT' THEN
   IF NEW.estado <> 'borrador' THEN RAISE EXCEPTION 'El documento debe iniciar en borrador'; END IF;
   RETURN NEW;
 END IF;
 IF OLD.estado='anulada' THEN RAISE EXCEPTION 'Documento anulado: no admite cambios'; END IF;
 IF OLD.estado='confirmada' THEN
   IF NEW.estado <> 'anulada' OR (to_jsonb(NEW)-'estado') <> (to_jsonb(OLD)-'estado') THEN
     RAISE EXCEPTION 'Un documento confirmado solo admite anulacion';
   END IF;
 ELSIF NEW.estado NOT IN ('borrador','confirmada','anulada') THEN
   RAISE EXCEPTION 'Transicion de estado no permitida';
 END IF;
 IF NEW.estado=OLD.estado THEN RETURN NEW; END IF;
 IF OLD.estado='borrador' AND NEW.estado='anulada' THEN RETURN NEW; END IF;

 IF TG_TABLE_NAME='compras' THEN
   IF NEW.estado='confirmada' AND NOT EXISTS (SELECT 1 FROM detalle_compras WHERE compra_id=NEW.id) THEN
     RAISE EXCEPTION 'Compra sin productos';
   END IF;
   IF NEW.estado='anulada' AND EXISTS
   (SELECT 1 FROM recepciones WHERE compra_id=NEW.id AND estado='confirmada') THEN
     RAISE EXCEPTION 'Anule primero las recepciones confirmadas';
   END IF;
 ELSIF TG_TABLE_NAME='recepciones' THEN
   PERFORM 1 FROM compras WHERE id=NEW.compra_id AND estado='confirmada' FOR UPDATE;
   IF NOT FOUND THEN RAISE EXCEPTION 'La compra debe estar confirmada'; END IF;
   PERFORM 1 FROM bodegas WHERE id=NEW.bodega_id FOR UPDATE;
   IF NOT EXISTS (SELECT 1 FROM detalle_recepciones WHERE recepcion_id=NEW.id) THEN
     RAISE EXCEPTION 'Recepcion sin productos';
   END IF;
   FOR r IN SELECT dr.*,dc.producto_id,dc.cantidad,dc.compra_id
   FROM detalle_recepciones dr JOIN detalle_compras dc ON dc.id=dr.detalle_compra_id
   WHERE dr.recepcion_id=NEW.id ORDER BY dc.producto_id LOOP
     IF r.compra_id <> NEW.compra_id THEN RAISE EXCEPTION 'El detalle pertenece a otra compra'; END IF;
     IF NEW.estado='confirmada' THEN
       SELECT COALESCE(sum(dr.cantidad_recibida),0) INTO recibida
       FROM detalle_recepciones dr JOIN recepciones re ON re.id=dr.recepcion_id
       WHERE dr.detalle_compra_id=r.detalle_compra_id AND re.estado='confirmada';
       IF recibida+r.cantidad_recibida > r.cantidad THEN RAISE EXCEPTION 'Cantidad recibida mayor a la comprada'; END IF;
     END IF;
     INSERT INTO movimientos_inventario(bodega_id,producto_id,tipo,cantidad,usuario_id,detalle_recepcion_id)
     VALUES(NEW.bodega_id,r.producto_id,
     CASE WHEN NEW.estado='confirmada' THEN 'recepcion' ELSE 'anulacion_recepcion' END,
     CASE WHEN NEW.estado='confirmada' THEN r.cantidad_recibida ELSE -r.cantidad_recibida END,
     NEW.recibido_por,r.id);
   END LOOP;
 ELSE -- ventas
   PERFORM 1 FROM bodegas WHERE id=NEW.bodega_id FOR UPDATE;
   IF NOT EXISTS (SELECT 1 FROM detalle_ventas WHERE venta_id=NEW.id) THEN RAISE EXCEPTION 'Venta sin productos'; END IF;
   IF NEW.estado='confirmada' THEN
     SELECT sum(subtotal) INTO total FROM detalle_ventas WHERE venta_id=NEW.id;
     SELECT COALESCE(sum(monto),0) INTO pagado FROM pagos_venta WHERE venta_id=NEW.id;
     IF total<>pagado THEN RAISE EXCEPTION 'El pago (%) debe coincidir con el total (%)',pagado,total; END IF;
   END IF;
   FOR r IN SELECT * FROM detalle_ventas WHERE venta_id=NEW.id ORDER BY producto_id LOOP
     INSERT INTO movimientos_inventario(bodega_id,producto_id,tipo,cantidad,usuario_id,detalle_venta_id)
     VALUES(NEW.bodega_id,r.producto_id,
     CASE WHEN NEW.estado='confirmada' THEN 'venta' ELSE 'anulacion_venta' END,
     CASE WHEN NEW.estado='confirmada' THEN -r.cantidad ELSE r.cantidad END,
     NEW.vendedor_id,r.id);
   END LOOP;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER procesar BEFORE INSERT OR UPDATE OR DELETE ON compras
 FOR EACH ROW EXECUTE FUNCTION procesar_documento();
CREATE TRIGGER procesar BEFORE INSERT OR UPDATE OR DELETE ON recepciones
 FOR EACH ROW EXECUTE FUNCTION procesar_documento();
CREATE TRIGGER procesar BEFORE INSERT OR UPDATE OR DELETE ON ventas
 FOR EACH ROW EXECUTE FUNCTION procesar_documento();

CREATE FUNCTION procesar_conteo() RETURNS trigger LANGUAGE plpgsql
SET search_path = optica, pg_catalog AS $$
DECLARE r record;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Cancele el conteo en lugar de eliminarlo'; END IF;
 IF TG_OP='INSERT' THEN
   IF NEW.estado <> 'abierto' THEN RAISE EXCEPTION 'El conteo debe iniciar abierto'; END IF;
   PERFORM 1 FROM bodegas WHERE id=NEW.bodega_id FOR UPDATE;
   RETURN NEW;
 END IF;
 IF OLD.estado <> 'abierto' THEN RAISE EXCEPTION 'Conteo finalizado: no admite cambios'; END IF;
 IF NEW.bodega_id<>OLD.bodega_id THEN RAISE EXCEPTION 'No cambie la bodega del conteo'; END IF;
 PERFORM 1 FROM bodegas WHERE id=NEW.bodega_id FOR UPDATE;
 IF NEW.estado='aprobado' THEN
   IF NEW.aprobado_por IS NULL THEN RAISE EXCEPTION 'Falta el responsable de aprobacion'; END IF;
   IF NOT EXISTS (SELECT 1 FROM detalle_conteos WHERE conteo_id=NEW.id) THEN RAISE EXCEPTION 'Conteo sin productos'; END IF;
   FOR r IN SELECT * FROM detalle_conteos WHERE conteo_id=NEW.id ORDER BY producto_id LOOP
     IF r.cantidad_contada IS NULL THEN RAISE EXCEPTION 'Hay productos sin contar'; END IF;
     IF r.diferencia<>0 THEN
       IF nullif(btrim(r.motivo),'') IS NULL THEN RAISE EXCEPTION 'Justifique cada diferencia'; END IF;
       IF r.cantidad_sistema <> COALESCE((SELECT cantidad FROM existencias
       WHERE bodega_id=NEW.bodega_id AND producto_id=r.producto_id),0) THEN
         RAISE EXCEPTION 'El corte de inventario no coincide con el saldo';
       END IF;
       INSERT INTO movimientos_inventario(bodega_id,producto_id,tipo,cantidad,usuario_id,detalle_conteo_id)
       VALUES(NEW.bodega_id,r.producto_id,'ajuste',r.diferencia,NEW.aprobado_por,r.id);
     END IF;
   END LOOP;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER procesar BEFORE INSERT OR UPDATE OR DELETE ON conteos_inventario
 FOR EACH ROW EXECUTE FUNCTION procesar_conteo();

CREATE FUNCTION iniciar_conteo(p_bodega bigint,p_usuario uuid,p_observaciones text DEFAULT NULL)
RETURNS bigint LANGUAGE plpgsql SET search_path = optica, pg_catalog AS $$
DECLARE nuevo bigint;
BEGIN
 PERFORM 1 FROM bodegas WHERE id=p_bodega FOR UPDATE;
 INSERT INTO conteos_inventario(bodega_id,creado_por,observaciones)
 VALUES(p_bodega,p_usuario,p_observaciones) RETURNING id INTO nuevo;
 INSERT INTO detalle_conteos(conteo_id,producto_id,cantidad_sistema)
 SELECT nuevo,p.id,COALESCE(e.cantidad,0) FROM productos p
 LEFT JOIN existencias e ON e.producto_id=p.id AND e.bodega_id=p_bodega
 WHERE p.activo OR COALESCE(e.cantidad,0)>0;
 RETURN nuevo;
END $$;

-- Consultas de uso frecuente. Los totales se calculan para no duplicar datos.
CREATE VIEW resumen_compras AS
 SELECT c.*,COALESCE(d.total,0) AS total FROM compras c LEFT JOIN
 (SELECT compra_id,sum(subtotal) AS total FROM detalle_compras GROUP BY compra_id) d ON d.compra_id=c.id;
CREATE VIEW resumen_ventas AS
 SELECT v.*,COALESCE(d.total,0) AS total,COALESCE(p.pagado,0) AS pagado
 FROM ventas v LEFT JOIN
 (SELECT venta_id,sum(subtotal) AS total FROM detalle_ventas GROUP BY venta_id) d ON d.venta_id=v.id
 LEFT JOIN (SELECT venta_id,sum(monto) AS pagado FROM pagos_venta GROUP BY venta_id) p ON p.venta_id=v.id;
CREATE VIEW inventario_actual AS
 SELECT b.id AS bodega_id,b.nombre AS bodega,p.id AS producto_id,p.codigo,p.nombre,
 COALESCE(e.cantidad,0) AS cantidad,p.stock_minimo,
 COALESCE(e.cantidad,0)<=p.stock_minimo AS stock_bajo
 FROM bodegas b CROSS JOIN productos p LEFT JOIN existencias e
 ON e.bodega_id=b.id AND e.producto_id=p.id;
CREATE VIEW pendientes_compra AS
 SELECT dc.id AS detalle_compra_id,dc.compra_id,dc.producto_id,dc.cantidad AS comprada,
 COALESCE(sum(dr.cantidad_recibida) FILTER (WHERE r.estado='confirmada'),0) AS recibida,
 dc.cantidad-COALESCE(sum(dr.cantidad_recibida) FILTER (WHERE r.estado='confirmada'),0) AS pendiente
 FROM detalle_compras dc LEFT JOIN detalle_recepciones dr ON dr.detalle_compra_id=dc.id
 LEFT JOIN recepciones r ON r.id=dr.recepcion_id GROUP BY dc.id;

CREATE INDEX ON perfiles(rol_id);
CREATE INDEX ON productos(categoria_id);
CREATE INDEX ON productos(marca_id);
CREATE INDEX ON clientes(telefono);
CREATE INDEX ON clientes(lower(nombres),lower(apellidos));
CREATE INDEX ON compras(proveedor_id,fecha);
CREATE INDEX ON compras(creado_por);
CREATE INDEX ON detalle_compras(producto_id);
CREATE INDEX ON recepciones(compra_id);
CREATE INDEX ON recepciones(bodega_id);
CREATE INDEX ON recepciones(recibido_por);
CREATE INDEX ON detalle_recepciones(detalle_compra_id);
CREATE INDEX ON existencias(producto_id);
CREATE INDEX ON ventas(cliente_id,fecha);
CREATE INDEX ON ventas(bodega_id);
CREATE INDEX ON ventas(vendedor_id);
CREATE INDEX ON detalle_ventas(producto_id);
CREATE INDEX ON conteos_inventario(creado_por);
CREATE INDEX ON conteos_inventario(aprobado_por);
CREATE INDEX ON detalle_conteos(producto_id);
CREATE INDEX ON movimientos_inventario(bodega_id,producto_id,fecha,id);
CREATE INDEX ON movimientos_inventario(producto_id);
CREATE INDEX ON movimientos_inventario(usuario_id);

INSERT INTO roles(nombre,descripcion) VALUES
 ('administrador','Administracion del sistema'),('vendedor','Registro de ventas'),
 ('bodeguero','Compras, recepciones y conteos');
INSERT INTO categorias(nombre) VALUES ('Monturas'),('Lentes'),('Accesorios');
INSERT INTO bodegas(nombre,ubicacion) VALUES ('Bodega principal','Sucursal principal');

REVOKE ALL ON ALL TABLES IN SCHEMA optica FROM PUBLIC;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA optica FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA optica FROM PUBLIC;
COMMIT;
