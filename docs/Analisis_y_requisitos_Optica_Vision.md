# Análisis y requisitos de Óptica Visión

**Versión de trabajo:** 0.1 · **País de referencia:** Nicaragua · **Fecha:** 14 de noviembre de 2026

## 1. Propósito y estado

Óptica Visión se plantea como un sistema administrativo para una óptica nicaragüense. La primera entrega se construye con ASP.NET Core y PostgreSQL en Supabase. El prototipo de Figma define la navegación y el diseño inicial: Inicio, Citas, Ventas, Compras, Inventario, Clientes, Proveedores y Reportes.

La interfaz ASP.NET Core ya está iniciada y muestra registros ficticios para desarrollar el diseño. **Todavía no tiene autenticación ni conexión operativa con Supabase; no debe utilizarse para guardar datos reales.** El proyecto de Supabase indicado por el propietario no se pudo inspeccionar desde la conexión disponible, por lo que no se aplicaron cambios a su base.

Este análisis separa los requisitos del producto de las obligaciones que corresponden a cada óptica cliente. La revisión es una base de diseño para el proyecto académico y no sustituye una revisión legal, tributaria o sanitaria antes de vender o poner el sistema en producción.

## 2. Alcance inicial

La primera versión contempla agenda de citas, clientes y datos de contacto mínimos, productos, proveedores, compras, recepción de inventario, ventas internas, pagos registrados, existencias y reportes operativos. Los documentos de compra y venta mantienen sus estados y sus movimientos de inventario conforme al modelo PostgreSQL existente.

En esta etapa, las ventas son registros internos del sistema. No se afirmará que el sistema emite facturas fiscales autorizadas. Tampoco se almacenarán recetas, diagnósticos, resultados de exámenes ni otra información clínica hasta definir si el cliente es un establecimiento proveedor de servicios de salud y revisar el alcance sanitario correspondiente.

## 3. Perfiles y permisos previstos

La autorización debe comprobarse en el servidor para cada operación. Ocultar un botón en la interfaz no constituye control de acceso.

| Perfil | Acceso previsto |
| --- | --- |
| Superadministrador de la plataforma | Administra empresas clientes, licencias, configuración técnica y soporte. No consulta por defecto los datos personales ni las operaciones de una óptica. Cualquier soporte que requiera ver datos deberá ser temporal, justificado y auditado. |
| Administrador de la óptica | Administra usuarios de su empresa, clientes, agenda, catálogo, proveedores, compras, ventas, inventario y reportes. No puede acceder a otra empresa. |
| Ventas | Registra ventas y pagos internos; consulta productos y existencias y solo los datos de cliente necesarios para la venta. No administra usuarios, permisos ni la plataforma. |
| Recepción | Registra y organiza citas y consulta los datos mínimos para contactar al cliente. No accede a caja, pagos, costos, compras ni reportes financieros. |
| Cliente | Inicia sesión en un portal y consulta o actualiza únicamente su perfil y sus propias citas. No ve a otros clientes ni datos internos de la óptica. |

Las altas, bajas, cambios de rol y operaciones sensibles deben guardar quién realizó el cambio, cuándo, desde qué cuenta y sobre qué registro. Las cuentas inactivas deben perder acceso. La contraseña nunca se guardará en texto plano.

### Política CRUD y conservación de registros

El CRUD ordinario será **crear, consultar, actualizar y desactivar**. No habrá una opción de borrado definitivo para usuarios, clientes, productos, proveedores, categorías, marcas ni bodegas. La desactivación conserva el registro y lo excluye de las listas operativas; la reactivación requiere permisos y auditoría. Las citas se cancelan conservando su historial. Las ventas, compras, recepciones y conteos confirmados se anulan o cancelan mediante estados y movimientos compensatorios cuando corresponda; sus detalles, pagos y movimientos no se eliminan. El usuario PostgreSQL de la aplicación no tendrá permiso ordinario `DELETE`.

Esta regla no impide atender solicitudes legales de cancelación o supresión de datos personales. Se atenderán mediante un procedimiento separado, verificando identidad y obligaciones de conservación, con acceso restringido y constancia de lo actuado; no mediante un botón de borrado en la operación diaria.

## 4. Requisitos funcionales

### 4.1 Identidad y acceso

- RF-01. Iniciar y cerrar sesión de forma segura.
- RF-02. Asignar uno de los perfiles definidos y aplicar sus permisos en el servidor.
- RF-03. Asociar cada cuenta y cada registro de negocio con la empresa cliente correspondiente.
- RF-04. Impedir que un usuario consulte, modifique o exporte información de otra empresa, incluso si altera una URL o identificador.
- RF-05. Permitir al superadministrador administrar empresas y licencias sin concederle acceso rutinario a los datos operativos.
- RF-06. Registrar eventos de seguridad y cambios administrativos sin escribir contraseñas, tokens ni datos sensibles en los logs.

### 4.2 Operación de la óptica

- RF-07. Registrar y consultar citas con fecha, hora, estado y datos de contacto mínimos.
- RF-08. Registrar clientes con finalidad informada y campos proporcionales al servicio.
- RF-09. Administrar productos, categorías, marcas, precios y niveles mínimos de existencias.
- RF-10. Registrar compras, recepción de productos y proveedores.
- RF-11. Registrar ventas internas y asociar líneas de productos y pagos conforme a las reglas del negocio.
- RF-12. Actualizar el inventario mediante movimientos trazables; impedir existencias negativas y no borrar movimientos ya confirmados.
- RF-13. Consultar reportes operativos filtrados por empresa, período y permisos.
- RF-14. Permitir búsqueda en listas sin exponer campos no autorizados.
- RF-15. Registrar aceptación, versión y fecha del aviso de privacidad cuando se recabe consentimiento.
- RF-16. Proporcionar procedimientos para atender solicitudes de acceso, rectificación, actualización, bloqueo o cancelación, sujetos a los plazos y excepciones aplicables.
- RF-17. Incorporar, si el cliente lo requiere, un módulo fiscal separado, habilitado solo después de que el contribuyente obtenga la autorización aplicable de la DGI.
- RF-18. Proveer CRUD para catálogos y registros operativos mediante alta, consulta, actualización y desactivación, conservando identificadores e historial.
- RF-19. Bloquear el borrado definitivo desde la aplicación; cancelar citas y anular documentos confirmados mediante estados y movimientos trazables.
- RF-20. Auditar altas, modificaciones, desactivaciones, reactivaciones y anulaciones con usuario, fecha y registro afectado.
- RF-21. Permitir a usuarios autorizados consultar y reactivar registros inactivos sin alterar el historial de operaciones.

## 5. Requisitos no funcionales

- RNF-01. **Confidencialidad:** HTTPS, contraseñas con hash seguro, permisos mínimos y secretos fuera del repositorio y del navegador.
- RNF-02. **Separación de empresas:** cada consulta, escritura, exportación, respaldo y tarea de soporte debe respetar el tenant de la cuenta autenticada. No se habilitará una instancia compartida comercialmente hasta diseñar y verificar esta separación en el esquema y en PostgreSQL.
- RNF-03. **Integridad:** transacciones para operaciones que afecten ventas, pagos e inventario; referencias válidas, restricciones y trazabilidad de anulaciones.
- RNF-04. **Disponibilidad y recuperación:** definir periodicidad de copias de respaldo, retención, recuperación y procedimiento de contingencia con el cliente.
- RNF-05. **Minimización:** recopilar solo los datos necesarios para citas, ventas, soporte y obligaciones de conservación.
- RNF-06. **Usabilidad:** interfaz en español, adaptable a computadora, tablet y teléfono mediante media queries, con controles accesibles y estados entendibles.
- RNF-07. **Mantenibilidad:** ASP.NET Core, PostgreSQL, migraciones versionadas, documentación de instalación y registro de versiones.
- RNF-08. **Portabilidad de datos:** exportación de información de la empresa cliente y un procedimiento de cierre y eliminación al finalizar el servicio, respetando obligaciones de conservación.
- RNF-09. **Auditoría:** bitácora protegida contra edición ordinaria para accesos administrativos, confirmaciones, anulaciones, cambios de permisos y acceso de soporte.
- RNF-10. **Conservación:** la conexión de la aplicación no tendrá permiso ordinario de borrado; las bajas serán lógicas y las transacciones se conservarán con su estado de anulación.

## 6. Base legal y decisiones de diseño

### 6.1 Datos personales

La Ley N.º 787 regula los ficheros de datos públicos y privados. Exige que la recolección sea adecuada, proporcional y necesaria; que se informe antes de recabar datos sobre la finalidad, el responsable, los destinatarios y los derechos; y que los datos se usen para la finalidad informada. Sus artículos 8 y 9 clasifican la información de salud como sensible y fijan condiciones para tratarla. El artículo 11 exige medidas técnicas y organizativas de seguridad; el 12 establece deber de confidencialidad; los artículos 17 y 19 prevén derechos de acceso y rectificación, entre otros; y los artículos 13 a 15 regulan cesiones y transferencias.

**Aplicación al diseño:** el formulario de cliente no pedirá información clínica. El módulo de privacidad permitirá informar finalidad, campos obligatorios y opcionales, responsable y canal de derechos. El sistema guardará consentimiento cuando corresponda y ofrecerá controles de acceso, auditoría, corrección, cancelación y exportación. La ubicación de Supabase y cualquier transferencia internacional deberán informarse y revisarse contractualmente antes de usar datos reales.

### 6.2 Facturación y obligaciones tributarias

La DGI publica los requisitos de facturación y el procedimiento para solicitar autorización de sistemas de facturación computarizados. La Disposición Técnica N.º 09-2007 describe información del sistema y su proveedor, control de acceso por usuario y contraseña, respaldos, manuales, numeración, formalidades, desglose de impuestos, anulación y contingencia; la DGI comunica que el contribuyente debe obtener autorización antes de usar el sistema para facturar.

**Aplicación al diseño:** la función actual se limitará a registrar ventas internas. Para activar facturación fiscal se deberán configurar por empresa la autorización DGI, identificación del proveedor, series, numeración, requisitos de factura, impuestos, anulaciones y contingencia. El sistema conservará los documentos anulados conforme a las reglas aplicables y no presentará un comprobante interno como factura fiscal.

### 6.3 Protección de consumidores

La Ley N.º 842 protege a las personas consumidoras y usuarias en el territorio nacional y reconoce el derecho a información veraz, oportuna, clara y relevante sobre los bienes y servicios. Contiene reglas para garantías y reclamos de bienes.

**Aplicación al diseño:** los módulos de venta podrán conservar descripción, precio, descuento, producto entregado y estado de garantía o reclamo. Los términos, garantía y comprobantes que vea el comprador deberán ser configurables y reflejar la política real del proveedor; no se inventarán condiciones legales en el programa.

### 6.4 Información clínica y establecimientos de salud

La Ley N.º 423 y la Normativa MINSA N.º 004 regulan el expediente clínico para establecimientos proveedores de servicios de salud públicos y privados. La normativa exige confidencialidad y acceso limitado al personal autorizado, y establece reglas de conservación y manejo del expediente.

**Aplicación al diseño:** la agenda comercial no equivale a un expediente clínico. Si el cliente realiza exámenes de refracción o conserva recetas como parte de atención de salud, ese módulo deberá diseñarse aparte con roles profesionales, registro clínico, conservación, acceso y revisión sanitaria apropiados. La clasificación concreta del establecimiento debe confirmarse con MINSA o asesoría local.

### 6.5 Derechos de autor y venta del software

La Ley N.º 312 incluye los programas de cómputo y la documentación técnica y manuales entre las obras protegidas por derecho de autor. MIFIC explica que la protección nace con la creación original; el registro puede servir como prueba ante controversias.

**Aplicación comercial:** preparar licencia de uso o contrato de suscripción, titularidad del código y manuales, alcance por empresa/sucursal/usuarios, mantenimiento, soporte, actualizaciones, copias, propiedad de los datos del cliente, exportación y terminación del servicio. Evitar incorporar fuentes, iconos, marcas, plantillas o bibliotecas sin licencia compatible. Considerar el registro del programa y sus versiones ante la oficina competente.

## 7. Arquitectura de datos prevista

La aplicación del navegador hablará con el servidor ASP.NET Core. El servidor autenticará a cada usuario, comprobará su empresa y rol, y conectará con PostgreSQL de Supabase con credenciales privadas. Ninguna clave de base de datos o `service_role` se incluirá en HTML, JavaScript, archivos públicos ni GitHub.

El esquema existente `optica` fue preparado para una empresa y deniega acceso público directo. Esto es una base segura para despliegue por empresa, pero todavía no implementa identidad ni aislamiento multiempresa. Antes de vender una instancia compartida se debe agregar y verificar el modelo de empresa/tenant y sus restricciones, o decidir una instalación aislada por empresa. El superadministrador debe usar un plano de control independiente y no una conexión privilegiada que le permita navegar los datos de cada óptica sin control.

La inspección de solo lectura del proyecto Supabase `amyjnztkqlxobpbreslj` encontró el esquema `optica` con 20 tablas y 4 vistas. Ya existe la tabla `citas`; los roles visibles son anteriores al conjunto acordado y hay perfiles vinculados a esos roles. No se ejecutaron escrituras ni cambios en la base. Por tanto, `optica_vision.sql` es solo una instalación inicial para una base vacía, no debe ejecutarse en este proyecto. Antes de conectar la aplicación se debe respaldar la base y preparar una migración que preserve IDs, perfiles, documentos y movimientos, cambie los roles de forma compatible y revise RLS y permisos.

## 8. Preparación para vender el sistema

Antes de ofrecerlo a una empresa, preparar:

1. Contrato/licencia y aceptación de alcance, precio, forma de pago, soporte, mantenimiento y renovación.
2. Aviso de privacidad y cláusulas que identifiquen a la óptica como responsable de los datos y al proveedor tecnológico según su rol real de tratamiento.
3. Procedimiento de alta de empresa, usuarios, roles, bajas, recuperación de acceso y soporte temporal auditado.
4. Plan de respaldo y restauración probado, contingencia eléctrica/Internet y exportación al terminar el contrato.
5. Manuales de usuario y técnico, inventario de versiones, licencias de dependencias y registro de autoría.
6. Procedimiento DGI por cada contribuyente si requiere facturación computarizada; la autorización de una óptica no debe presumirse válida para otra empresa.
7. Revisión legal, tributaria y sanitaria con profesionales locales antes de afirmar cumplimiento normativo o comercializar funciones fiscales o clínicas.

## 9. Fuentes oficiales consultadas

- [Ley N.º 787, La Gaceta N.º 61, 29 de marzo de 2012](https://sajurin.enriquebolanos.org/docs/G-2012-03-29.pdf)
- [Reglamento de la Ley N.º 787, Decreto 36-2012](https://nicaragua.justia.com/nacionales/decretos-ejecutivos/reglamento-de-la-ley-no-787-ley-de-proteccion-de-datos-personales-oct-19-2012/gdoc/)
- [DGI, requisitos para autorización de sistema de facturación computarizada](https://www.dgi.gob.ni/FAQ/otros_tramites.htm)
- [DGI, requisitos de facturas y obligaciones de responsables recaudadores](https://www.dgi.gob.ni/FAQ/obligaciones_de_los_responsabl.htm)
- [Ley N.º 842, MIFIC](https://www.mific.gob.ni/Portals/0/Documentos/OAIP/DOCUMENTOS/06-DIPRODEC%20LEY%20N_%20842%20.pdf)
- [Normativa MINSA N.º 004, manejo del expediente clínico](https://www.minsa.gob.ni/sites/default/files/2023-02/Normativa%20-%20004%20%27Norma%20para%20el%20manejo%20del%20expediente%20cl%C3%ADnico%27.pdf)
- [MIFIC, Derechos de Autor y Derechos Conexos](https://www.mific.gob.ni/language/en-US/Inicio/Fomento/DGRPI/Derechos-de-Autor)
- [Texto consolidado de Ley N.º 312, WIPO Lex](https://wipolex-res.wipo.int/edocs/lexdocs/laws/es/ni/ni053es.pdf)

**Nota:** las normas y criterios administrativos pueden cambiar. Antes de la puesta en producción, confirmar con DGI, MINSA y asesoría legal nicaragüense la normativa vigente y su aplicación al tipo concreto de óptica y servicio.
