# Manual técnico del sistema Óptica Visión

**Versión:** 0.2 — **País de referencia:** Nicaragua — **Fecha:** 14 de noviembre de 2026  
**Tipo de entrega:** prototipo académico en desarrollo; no es una versión autorizada para operación comercial.

## 1. Objetivo

Óptica Visión organiza la agenda y las operaciones administrativas de una óptica. El sistema se desarrolla como una aplicación web ASP.NET Core MVC, con PostgreSQL gestionado en Supabase. El prototipo visual inicial está en [Figma](https://www.figma.com/proto/BUID7VzwJXygprW9ZIiKhs/Optica-Vision?node-id=2-2&page-id=0%3A1&starting-point-node-id=2%3A2&t=Z9BQRFoadSdQ7lXB-1).

La meta de la defensa es demostrar el análisis, la arquitectura, las decisiones de seguridad y los módulos implementados. Este documento distingue funciones reales en código de pantallas todavía demostrativas.

## 2. Alcance

El alcance funcional previsto comprende usuarios y permisos, clientes, citas, catálogo de productos, proveedores, compras, recepción, inventario, ventas internas, pagos registrados y reportes. La primera entrega conectada comprende autenticación/perfiles, administración básica de usuarios y CRUD de clientes. Los demás módulos todavía requieren conexión a datos y reglas de negocio antes de presentarlos como operaciones reales.

El registro de una venta no constituye por sí mismo una factura fiscal. No se incluyen emisión fiscal, notas de crédito, devoluciones parciales ni historia clínica en la primera entrega.

## 3. Requerimientos funcionales

1. Iniciar y cerrar sesión con cuentas gestionadas por Supabase Auth.
2. Asignar perfiles y comprobar permisos en el servidor, no solo en la interfaz.
3. Crear usuarios y asignarles un rol autorizado; exigir cambio de contraseña temporal en el primer inicio de sesión.
4. Consultar, crear, actualizar, desactivar y reactivar clientes; conservar su identificador e historial.
5. Buscar registros y permitir que personal autorizado consulte los inactivos.
6. Registrar citas con cliente, fecha, servicio, estado y notas; cancelarlas conservando el historial.
7. Gestionar productos, categorías, marcas, proveedores y bodegas mediante desactivación, no borrado ordinario.
8. Registrar compras y recepciones; confirmar la recepción únicamente después de validar cantidades.
9. Registrar ventas y pagos internos; confirmar solo si el pago y la existencia satisfacen las reglas del negocio.
10. Conservar compras, ventas, pagos y movimientos confirmados; corregir mediante anulación y movimientos compensatorios.
11. Auditar altas, cambios, bajas lógicas, reactivaciones y operaciones sensibles.
12. Proporcionar reportes filtrados por período, empresa y rol.

## 4. Requerimientos no funcionales

- Seguridad: HTTPS, cookies HttpOnly, protección antifalsificación, validación del lado servidor, consultas parametrizadas y contraseñas gestionadas por Supabase Auth.
- Secretos: conexión PostgreSQL, publishable key, secret key y token inicial solo en configuración privada del servidor.
- Privacidad: minimizar datos personales, informar finalidades, limitar acceso por función y registrar eventos administrativos.
- Integridad: transacciones de base de datos para cambios relacionados; referencias e invariantes en PostgreSQL.
- Conservación: desactivación lógica en catálogos y personas; anulaciones para documentos; ausencia de funciones normales de borrado físico.
- Compatibilidad: ASP.NET Core 10, PostgreSQL y diseño web adaptable mediante media queries.
- Mantenibilidad: SQL versionado, documentación de despliegue y mensajes de error que no revelen secretos.
- Disponibilidad: definir respaldo, retención, recuperación y procedimiento de contingencia antes de producción.

## 5. Análisis del sistema

### 5.1 Actores

| Rol | Función principal | Límites de acceso |
|---|---|---|
| Superadministrador | Configuración de plataforma y soporte | No consulta rutinariamente información de clientes de la óptica. |
| Administrador | Usuarios y operaciones de su óptica | No accede a otra empresa. |
| Ventas | Venta interna y consulta de productos/stock | No administra cuentas ni configuración. |
| Recepción | Agenda y contacto mínimo con clientes | No accede a costos, caja ni reportes financieros. |
| Cliente | Consulta de su perfil y citas | Solo sus propios datos; portal aún pendiente. |

### 5.2 Procesos y reglas

- Una cuenta desactivada no puede autenticarse; su perfil queda conservado.
- Un usuario administrador no puede crear otro administrador ni superadministrador. La administración de roles de plataforma queda reservada al superadministrador.
- Una cita cancelada permanece visible en el historial.
- Los documentos comerciales comienzan como borrador; las confirmaciones deben actualizar inventario en una transacción.
- Los movimientos de inventario no se editan ni se eliminan. Las anulaciones usan movimientos compensatorios.
- La versión del esquema inicial de base de datos usa las restricciones y triggers documentados en [LEEME_optica.md](../LEEME_optica.md).

## 6. Diseño del sistema

### 6.1 Arquitectura

```text
Navegador
  -> ASP.NET Core MVC (controladores, validación, roles y cookies)
      -> Supabase Auth (inicio de sesión y cuentas)
      -> PostgreSQL de Supabase (datos del negocio y auditoría, mediante Npgsql)
```

El navegador no accede directamente a las tablas de negocio. El backend se conecta mediante TLS y usa consultas parametrizadas. La sesión local se valida contra el estado activo del perfil antes de autorizar cada solicitud.

### 6.2 Modelo de datos

El esquema inicial `optica` contiene perfiles y roles, clientes, citas, proveedores, catálogos de productos, bodegas, compras, recepción, existencias, ventas, pagos, conteos, movimientos de inventario y vistas de resumen. La bitácora `auditoria_eventos` conserva operaciones administrativas. El documento [Diccionario_Datos_Optica_Vision.xlsx](../Diccionario_Datos_Optica_Vision.xlsx) complementa los campos y relaciones.

La base Supabase existente necesita columnas de enlace con Auth, estado de roles y bitácora. La preparación incremental está en `docs/Preparar_Supabase_Existente.sql`; no reemplaza ni reinstala el esquema existente.

### 6.3 Interfaz

La interfaz toma como referencia el prototipo Figma y se adapta a escritorio y pantallas pequeñas. Las vistas de Clientes y Usuarios representan flujos conectados a datos una vez se configure la base. Las vistas de Citas, Ventas, Compras, Inventario, Proveedores y Reportes continúan siendo maquetas y muestran datos ficticios.

## 7. Implementación disponible

| Componente | Estado |
|---|---|
| ASP.NET Core MVC | Estructura del sitio, navegación y formularios. |
| Login con Supabase Auth | Código preparado; requiere credenciales privadas y perfil vinculado. |
| Roles y usuarios | Alta, asignación restringida, baja lógica y reactivación; requiere aplicar la preparación SQL. |
| Clientes | Listado, búsqueda, alta, consulta, edición, desactivación/reactivación y auditoría. |
| Citas | Vista demostrativa; CRUD real pendiente. |
| Ventas, compras, inventario, proveedores y reportes | Vistas demostrativas; operaciones reales pendientes. |
| PostgreSQL/Supabase | Esquema inicial y script incremental preparados; script incremental aún no aplicado al proyecto remoto. |
| GitHub | Código en el repositorio de trabajo local; los cambios de esta sesión no se han podido confirmar ni enviar al remoto. |
| Publicación web | Pendiente. |

## 8. Instalación y configuración

1. Respaldar el proyecto Supabase existente.
2. Revisar y aplicar una sola vez `docs/Preparar_Supabase_Existente.sql` en el SQL Editor de Supabase. No ejecutar allí el esquema de instalación inicial.
3. Desde el panel **Connect** de Supabase, obtener la cadena PostgreSQL de Session pooler y copiar URL, publishable key y secret key de Auth.
4. Guardar los cuatro valores en User Secrets del proyecto `src/OpticaVision.Web`; añadir también `Bootstrap:Token` temporal, aleatorio y largo.
5. Restaurar dependencias con Internet y ejecutar `dotnet run --project src/OpticaVision.Web`.
6. Abrir `/configuracion-inicial`, crear la primera cuenta superadministradora y retirar el token temporal de la configuración.
7. Iniciar sesión, probar los roles con cuentas ficticias y verificar la bitácora antes de cualquier demostración.

Visual Studio 2022 no admite oficialmente el destino .NET 10. Para editar/ejecutar este destino se necesita Visual Studio 2026 o usar la terminal/VS Code con .NET 10 SDK. No copiar secretos al repositorio.

## 9. Seguridad, privacidad y normativa de Nicaragua

El análisis de base legal, fuentes y límites funcionales está en [Analisis_y_requisitos_Optica_Vision.md](Analisis_y_requisitos_Optica_Vision.md). El diseño considera la Ley 787 de protección de datos personales, la Ley 842 de protección de las personas consumidoras y la normativa sanitaria solo si el negocio presta servicios de salud. La aplicación no declara conformidad legal certificada.

Antes de comercializar: validar aviso de privacidad y gestión de derechos, determinar responsable/encargado y transferencias internacionales, configurar retención y copias de seguridad, revisar controles de tenant por empresa, evaluar obligaciones de la DGI y obtener revisión legal/tributaria/sanitaria aplicable. La conexión multempresa y el portal cliente no están completos en esta entrega.

## 10. Verificación y defensa

Casos mínimos de demostración: inicio de sesión correcto e incorrecto; bloqueo de cuenta desactivada; acceso denegado por rol; alta/edición/desactivación/reactivación de cliente; preservación de una cuenta desactivada; y registro de cada acción en auditoría. Usar datos ficticios y mostrar el prototipo Figma junto con la arquitectura y las limitaciones.

No se deben afirmar despliegue, conexión operativa remota, cumplimiento tributario ni módulos conectados hasta que esos puntos se verifiquen de manera efectiva.
