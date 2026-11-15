-- Datos sinteticos para presentar Optica Vision en una base local/de pruebas.
-- NO ejecutar en el proyecto Supabase de produccion: crea clientes, compras,
-- recepciones, ventas y movimientos de inventario ficticios.
-- Para habilitarlo en una sesion local, ejecute primero:
--   SET app.optica_demo_seed = 'enabled';
-- Este script no crea usuarios de Auth ni perfiles de cliente.

BEGIN;

DO $demo$
<<seed_block>>
DECLARE
    i integer;
    total integer;
    actor_id uuid;
    cliente_id bigint;
    proveedor_id bigint;
    categoria_id bigint;
    marca_id bigint;
    producto_id bigint;
    bodega_id bigint;
    compra_id bigint;
    detalle_compra_id bigint;
    recepcion_id bigint;
    venta_id bigint;
    detalle_venta_id bigint;
    precio numeric(14,2);
BEGIN
    IF current_setting('app.optica_demo_seed', true) IS DISTINCT FROM 'enabled' THEN
        RAISE EXCEPTION 'Este conjunto solo es para desarrollo. Active primero SET app.optica_demo_seed = ''enabled'' en la sesion local.';
    END IF;

    SELECT p.id INTO actor_id
    FROM optica.perfiles p
    JOIN optica.roles r ON r.id = p.rol_id
    WHERE p.activo AND r.activo AND r.nombre = 'administrador'
    ORDER BY p.creado_en, p.id
    LIMIT 1;

    IF actor_id IS NULL THEN
        RAISE EXCEPTION 'Cree primero un perfil administrativo de prueba para relacionar las operaciones.';
    END IF;

    INSERT INTO optica.roles(nombre, descripcion)
    VALUES ('bodega', 'Gestiona recepciones, existencias y el kardex de inventario.')
    ON CONFLICT (nombre) DO NOTHING;

    -- Completa catálogos de demostracion hasta diez; no duplica nombres.
    i := 1;
    LOOP
        SELECT count(*) INTO total FROM optica.categorias;
        EXIT WHEN total >= 10;
        INSERT INTO optica.categorias(nombre, descripcion)
        VALUES (format('Categoria demo %s', lpad(i::text, 2, '0')), 'Registro sintetico para demostracion')
        ON CONFLICT (nombre) DO NOTHING;
        i := i + 1;
    END LOOP;

    i := 1;
    LOOP
        SELECT count(*) INTO total FROM optica.marcas;
        EXIT WHEN total >= 10;
        INSERT INTO optica.marcas(nombre)
        VALUES (format('Marca demo %s', lpad(i::text, 2, '0')))
        ON CONFLICT (nombre) DO NOTHING;
        i := i + 1;
    END LOOP;

    FOR i IN 1..10 LOOP
        INSERT INTO optica.bodegas(nombre, ubicacion)
        VALUES (format('Bodega demo %s', lpad(i::text, 2, '0')), 'Ubicacion ficticia de demostracion')
        ON CONFLICT (nombre) DO NOTHING;
    END LOOP;

    -- Clientes, proveedores y productos sinteticos, sin cuentas de inicio de sesion.
    FOR i IN 1..10 LOOP
        INSERT INTO optica.clientes(codigo, nombres, apellidos, telefono, correo, direccion)
        VALUES (format('DEMO-CLI-%s', lpad(i::text, 3, '0')), format('Cliente demo %s', i), 'Optica',
                format('0000-%s', lpad(i::text, 4, '0')), format('cliente%s@example.invalid', i), 'Direccion ficticia')
        ON CONFLICT (codigo) DO NOTHING;

        INSERT INTO optica.proveedores(nombre, identificacion_fiscal, contacto, telefono, correo, direccion)
        VALUES (format('Proveedor demo %s', lpad(i::text, 2, '0')), format('DEMO-RUC-%s', lpad(i::text, 3, '0')),
                format('Contacto demo %s', i), format('0000-%s', lpad((1000 + i)::text, 4, '0')),
                format('proveedor%s@example.invalid', i), 'Direccion ficticia')
        ON CONFLICT (identificacion_fiscal) DO NOTHING;

        SELECT id INTO categoria_id FROM optica.categorias ORDER BY id OFFSET ((i - 1) % GREATEST((SELECT count(*) FROM optica.categorias), 1)) LIMIT 1;
        SELECT id INTO marca_id FROM optica.marcas ORDER BY id OFFSET ((i - 1) % GREATEST((SELECT count(*) FROM optica.marcas), 1)) LIMIT 1;
        INSERT INTO optica.productos(codigo, nombre, categoria_id, marca_id, modelo, color, descripcion, precio_venta, stock_minimo)
        VALUES (format('DEMO-PROD-%s', lpad(i::text, 3, '0')), format('Producto demo %s', lpad(i::text, 2, '0')),
                categoria_id, marca_id, format('Modelo demo %s', i), 'Negro', 'Producto sintetico para presentacion',
                500 + (i * 75), 3)
        ON CONFLICT (codigo) DO NOTHING;

        SELECT id INTO cliente_id FROM optica.clientes WHERE codigo = format('DEMO-CLI-%s', lpad(i::text, 3, '0'));
        IF NOT EXISTS (SELECT 1 FROM optica.citas WHERE notas = format('DEMO-SEED: cita %s', lpad(i::text, 2, '0'))) THEN
            INSERT INTO optica.citas(cliente_id, fecha, servicio, estado, notas)
            VALUES (cliente_id, now() + (i || ' days')::interval, 'Examen visual', 'pendiente',
                    format('DEMO-SEED: cita %s', lpad(i::text, 2, '0')));
        END IF;
    END LOOP;

    -- Genera operaciones completas para que el inventario y los kardex sean coherentes.
    FOR i IN 1..10 LOOP
        SELECT id INTO proveedor_id FROM optica.proveedores WHERE identificacion_fiscal = format('DEMO-RUC-%s', lpad(i::text, 3, '0'));
        SELECT id INTO producto_id FROM optica.productos WHERE codigo = format('DEMO-PROD-%s', lpad(i::text, 3, '0'));
        SELECT id INTO bodega_id FROM optica.bodegas WHERE nombre = format('Bodega demo %s', lpad(i::text, 2, '0'));

        SELECT c.id INTO compra_id FROM optica.compras c
        WHERE c.proveedor_id = seed_block.proveedor_id AND c.numero_documento_proveedor = format('DEMO-COM-%s', lpad(i::text, 3, '0'));
        IF compra_id IS NULL THEN
            INSERT INTO optica.compras(proveedor_id, numero_documento_proveedor, fecha, estado, creado_por, observaciones)
            VALUES (proveedor_id, format('DEMO-COM-%s', lpad(i::text, 3, '0')), current_date - i, 'borrador', actor_id, 'Operacion sintetica de demostracion')
            RETURNING id INTO compra_id;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM optica.detalle_compras dc WHERE dc.compra_id = seed_block.compra_id AND dc.producto_id = seed_block.producto_id) THEN
            INSERT INTO optica.detalle_compras(compra_id, producto_id, cantidad, costo_unitario)
            VALUES (compra_id, producto_id, 20, 250 + (i * 25));
        END IF;
        UPDATE optica.compras SET estado = 'confirmada' WHERE id = compra_id AND estado = 'borrador';

        SELECT dc.id INTO detalle_compra_id FROM optica.detalle_compras dc
        WHERE dc.compra_id = seed_block.compra_id AND dc.producto_id = seed_block.producto_id LIMIT 1;
        SELECT id INTO recepcion_id FROM optica.recepciones
        WHERE observaciones = format('DEMO-SEED: recepcion %s', lpad(i::text, 2, '0'));
        IF recepcion_id IS NULL THEN
            INSERT INTO optica.recepciones(compra_id, bodega_id, fecha, estado, recibido_por, observaciones)
            VALUES (compra_id, bodega_id, current_date - i, 'borrador', actor_id,
                    format('DEMO-SEED: recepcion %s', lpad(i::text, 2, '0')))
            RETURNING id INTO recepcion_id;
        END IF;
        IF NOT EXISTS (SELECT 1 FROM optica.detalle_recepciones dr WHERE dr.recepcion_id = seed_block.recepcion_id AND dr.detalle_compra_id = seed_block.detalle_compra_id) THEN
            INSERT INTO optica.detalle_recepciones(recepcion_id, detalle_compra_id, cantidad_recibida)
            VALUES (recepcion_id, detalle_compra_id, 20);
        END IF;
        UPDATE optica.recepciones SET estado = 'confirmada' WHERE id = recepcion_id AND estado = 'borrador';

        SELECT precio_venta INTO precio FROM optica.productos WHERE id = producto_id;
        SELECT id INTO cliente_id FROM optica.clientes WHERE codigo = format('DEMO-CLI-%s', lpad(i::text, 3, '0'));
        SELECT v.id INTO venta_id FROM optica.ventas v
        WHERE v.cliente_id = seed_block.cliente_id AND v.vendedor_id = seed_block.actor_id AND v.bodega_id = seed_block.bodega_id
          AND v.fecha::date = (current_date - i);
        IF venta_id IS NULL THEN
            INSERT INTO optica.ventas(cliente_id, bodega_id, fecha, estado, vendedor_id)
            VALUES (cliente_id, bodega_id, current_date - i, 'borrador', actor_id)
            RETURNING id INTO venta_id;
        END IF;
        IF NOT EXISTS (SELECT 1 FROM optica.detalle_ventas dv WHERE dv.venta_id = seed_block.venta_id AND dv.producto_id = seed_block.producto_id) THEN
            INSERT INTO optica.detalle_ventas(venta_id, producto_id, cantidad, precio_unitario, descuento_unitario)
            VALUES (venta_id, producto_id, 1, precio, 0);
        END IF;
        SELECT dv.id INTO detalle_venta_id FROM optica.detalle_ventas dv WHERE dv.venta_id = seed_block.venta_id AND dv.producto_id = seed_block.producto_id LIMIT 1;
        IF NOT EXISTS (SELECT 1 FROM optica.pagos_venta pv WHERE pv.venta_id = seed_block.venta_id) THEN
            INSERT INTO optica.pagos_venta(venta_id, metodo, monto, referencia)
            VALUES (venta_id, CASE WHEN i % 3 = 0 THEN 'tarjeta' WHEN i % 3 = 1 THEN 'efectivo' ELSE 'transferencia' END, precio,
                    format('DEMO-PAGO-%s', lpad(i::text, 3, '0')));
        END IF;
        UPDATE optica.ventas SET estado = 'confirmada' WHERE id = venta_id AND estado = 'borrador';

        IF NOT EXISTS (SELECT 1 FROM optica.conteos_inventario WHERE observaciones = format('DEMO-SEED: conteo %s', lpad(i::text, 2, '0'))) THEN
            PERFORM optica.iniciar_conteo(bodega_id, actor_id, format('DEMO-SEED: conteo %s', lpad(i::text, 2, '0')));
        END IF;
    END LOOP;
END
$demo$;

COMMIT;

-- Conteos posteriores: las vistas son derivadas y no se insertan directamente.
SELECT 'roles' AS entidad, count(*) AS registros FROM optica.roles
UNION ALL SELECT 'perfiles', count(*) FROM optica.perfiles
UNION ALL SELECT 'clientes', count(*) FROM optica.clientes
UNION ALL SELECT 'citas', count(*) FROM optica.citas
UNION ALL SELECT 'proveedores', count(*) FROM optica.proveedores
UNION ALL SELECT 'categorias', count(*) FROM optica.categorias
UNION ALL SELECT 'marcas', count(*) FROM optica.marcas
UNION ALL SELECT 'productos', count(*) FROM optica.productos
UNION ALL SELECT 'bodegas', count(*) FROM optica.bodegas
UNION ALL SELECT 'compras', count(*) FROM optica.compras
UNION ALL SELECT 'detalle_compras', count(*) FROM optica.detalle_compras
UNION ALL SELECT 'recepciones', count(*) FROM optica.recepciones
UNION ALL SELECT 'detalle_recepciones', count(*) FROM optica.detalle_recepciones
UNION ALL SELECT 'existencias', count(*) FROM optica.existencias
UNION ALL SELECT 'ventas', count(*) FROM optica.ventas
UNION ALL SELECT 'detalle_ventas', count(*) FROM optica.detalle_ventas
UNION ALL SELECT 'pagos_venta', count(*) FROM optica.pagos_venta
UNION ALL SELECT 'conteos_inventario', count(*) FROM optica.conteos_inventario
UNION ALL SELECT 'detalle_conteos', count(*) FROM optica.detalle_conteos
UNION ALL SELECT 'movimientos_inventario', count(*) FROM optica.movimientos_inventario
UNION ALL SELECT 'auditoria_eventos', count(*) FROM optica.auditoria_eventos
UNION ALL SELECT 'resumen_compras', count(*) FROM optica.resumen_compras
UNION ALL SELECT 'resumen_ventas', count(*) FROM optica.resumen_ventas
UNION ALL SELECT 'inventario_actual', count(*) FROM optica.inventario_actual
UNION ALL SELECT 'pendientes_compra', count(*) FROM optica.pendientes_compra
ORDER BY entidad;
