# Base de datos - Optica Vision

## Instalacion

1. Abra SQL Editor en su proyecto Supabase.
2. Pegue el contenido completo de `optica_vision.sql` y ejecutelo una sola vez.
3. Las 19 tablas se crean en el esquema `optica`, no en `public`.
4. El script incluye roles, categorias y una bodega inicial. No incluye personas, contrasenas ni transacciones de ejemplo.

La instalacion es transaccional: un error revierte la instalacion completa. No vuelva a ejecutar el archivo sobre el esquema existente; no es una migracion ni borra tablas anteriores.

## Autenticacion y acceso

Esta entrega es la base de datos comercial para una aplicacion ASP.NET Core que se conecta desde su servidor. El esquema no concede acceso al navegador ni expone funciones publicas. No configure el esquema como expuesto en la API de Supabase sin definir primero permisos y politicas RLS.

`perfiles` identifica a empleados y su rol comercial. No implementa inicio de sesion ni guarda contrasenas. Falta elegir con el equipo entre ASP.NET Core Identity y Supabase Auth. Si usan Supabase Auth, el identificador de perfil puede ser el UUID de `auth.users` y se debe agregar su relacion al integrar la autenticacion.

La tabla `roles` no equivale a roles del servidor PostgreSQL. El backend debe autenticar al empleado, comprobar permisos por operacion, verificar que este activo y asignar su identificador como responsable. Los campos de responsables no verifican automaticamente quien hizo la peticion. Para anulaciones, los movimientos conservan el responsable original del documento; una auditoria del usuario que anula requiere ampliar esta version.

Antes de conectar la aplicacion, crear un usuario PostgreSQL propio para el servidor y concederle solo los permisos necesarios sobre este esquema. No usar la cuenta propietaria ni una clave de servicio en el navegador. La configuracion de permisos y autenticacion queda pendiente hasta elegir el mecanismo de acceso.

## Flujo comercial

Las operaciones empiezan como borrador. Agregue la cabecera, detalles y, en ventas, pagos. Luego confirme cambiando el estado. Los triggers ejecutan el movimiento y el cambio de estado en la misma transaccion.

```sql
-- Sustituya los identificadores por los de sus documentos.
UPDATE optica.compras SET estado = 'confirmada' WHERE id = 1;
UPDATE optica.recepciones SET estado = 'confirmada' WHERE id = 1;
UPDATE optica.ventas SET estado = 'confirmada' WHERE id = 1;
```

- Una compra confirmada no aumenta existencias.
- Una recepcion confirmada aumenta existencias y valida la cantidad pendiente de compra.
- Una venta confirmada exige pago exacto y stock suficiente. Una venta con total cero puede confirmarse sin pagos.
- El precio de venta se guarda en el detalle: la aplicacion debe copiar el precio del producto al agregarlo.
- Para anular use `estado = 'anulada'`. Una recepcion confirmada solo puede anularse si existe stock suficiente para retirar lo recibido.
- No hay transicion de anulada a confirmada. Cree un documento nuevo si necesita repetir una operacion.
- Una compra no se anula mientras tenga recepciones confirmadas.
- Las ventas anuladas conservan los pagos historicos; no representan ingresos vigentes. El reporte debe filtrar por ventas confirmadas. No hay procesamiento de reembolsos bancarios.
- Anulacion completa no equivale a devolucion parcial: devoluciones y notas de credito quedan fuera de esta version.
- Las cantidades son unidades enteras, el dinero usa dos decimales y se asume una sola moneda. No incluye impuestos, credito ni facturacion fiscal.
- Los movimientos usan cantidad positiva para entradas y negativa para salidas. No se editan ni se eliminan.

## Conteo fisico

```sql
-- Devuelve el ID del conteo y toma una fotografia del saldo por producto.
SELECT optica.iniciar_conteo(
  1, 'UUID-REAL-DEL-EMPLEADO'::uuid, 'Conteo mensual'
);

-- Registre la cantidad real por linea, incluso cero si corresponde.
UPDATE optica.detalle_conteos
SET cantidad_contada = 8, motivo = 'Faltante verificado'
WHERE id = 1;

UPDATE optica.conteos_inventario
SET estado = 'aprobado', aprobado_por = 'UUID-REAL-DEL-EMPLEADO'::uuid
WHERE id = 1;

-- Para abandonar un conteo abierto:
-- UPDATE optica.conteos_inventario SET estado='cancelado' WHERE id=1;
```

Durante un conteo abierto se bloquean movimientos comerciales de esa bodega. Esto permite un corte coherente para una sucursal pequena. Las ventas pueden guardarse como borrador y confirmarse despues. Toda diferencia necesita motivo. La aprobacion exige contar todas las lineas y registra los ajustes necesarios. Para inventario inicial, use un conteo aprobado con justificacion de apertura.

Use siempre `iniciar_conteo` para crear el corte; no inserte ni elimine manualmente sus lineas. Si agregan productos durante el conteo, deben completar otro conteo posterior si necesitan ajustar esos productos.

## Consultas

```sql
SELECT * FROM optica.inventario_actual ORDER BY codigo;
SELECT * FROM optica.inventario_actual WHERE stock_bajo;
SELECT * FROM optica.pendientes_compra WHERE pendiente > 0;
SELECT * FROM optica.resumen_ventas WHERE estado = 'confirmada';
SELECT * FROM optica.resumen_compras WHERE estado = 'confirmada';
SELECT * FROM optica.movimientos_inventario
WHERE bodega_id = 1 AND producto_id = 1 ORDER BY fecha, id;
```

`pendientes_compra` muestra cantidades por linea; para reportar solo compras vigentes, unir con compras y filtrar su estado confirmado.

## Concurrencia y mantenimiento

Los movimientos bloquean la fila de bodega durante la transaccion y actualizan el saldo con una restriccion que impide negativos. Esto serializa los movimientos de una bodega; es una decision sencilla para el alcance universitario. Mantenga las transacciones cortas. El backend debe gestionar reintentos ante interbloqueos o errores de serializacion y presentar mensajes entendibles.

Los usuarios propietarios de la base pueden alterar o desactivar las protecciones: no son la cuenta recomendada para la aplicacion. El sistema no incluye sincronizacion sin conexion, restauracion automatica ni configuracion de copias de seguridad. Esos servicios se deben definir aparte.
