# Lista de entrega del examen

## Datos que se completan en clase

- Integrante 1: ______________________________________________
- Integrante 2: ______________________________________________
- Integrante 3: ______________________________________________
- Número de proyecto asignado por rifa: _______________________

Los nombres se anotan en media hoja de papel y el número se completa cuando lo asigne el profesor. No se inventan esos datos.

## Requisitos del proyecto

| N.º | Entregable solicitado | Evidencia para presentar | Estado actual |
|---:|---|---|---|
| 3 | Manual | Requisitos funcionales/no funcionales, análisis, diseño, Figma, implementación, PostgreSQL/Supabase y ASP.NET Core. | Manual técnico y análisis preparados; completar integrantes y número del proyecto. |
| 4 | Repositorio GitHub | Repositorio compartido con el código y documentos del equipo. | Hay cambios en la carpeta local; aún no están confirmados ni enviados al remoto. |
| 5 | Base de datos en Supabase | Esquema conectado a la aplicación y evidencia de inserción/consulta. | Script incremental preparado; aún no aplicado al proyecto remoto. |
| 6 | Sitio web publicado | URL pública y prueba de acceso. | Pendiente. |
| 7 | Documento impreso | Manual final revisado e impreso. | El manual está en Markdown; falta preparar la versión para imprimir. |
| 8 | Defensa el día del examen | Demostración funcional y explicación del análisis, diseño, base y tecnologías. | Preparar cuando la conexión y los módulos estén verificados. |

## Distribución de la nota

| Componente | Puntos | Preparación necesaria |
|---|---:|---|
| Defensa | 5 | Explicar decisiones del sistema y demostrar los flujos implementados. |
| Documento | 5 | Imprimir el manual con los nombres y el número de proyecto asignado. |
| Proyecto | 10 | Presentar aplicación, repositorio compartido, base conectada y sitio publicado. |
| **Total** | **20** | |

## Contenido mínimo del manual

1. Requerimientos funcionales y no funcionales.
2. Análisis del sistema: problema, alcance, actores y reglas.
3. Diseño del sistema: arquitectura, modelo de datos, roles y diagramas.
4. Prototipo de Figma: [Óptica Visión](https://www.figma.com/proto/BUID7VzwJXygprW9ZIiKhs/Optica-Vision?node-id=2-2&page-id=0%3A1&starting-point-node-id=2%3A2&t=Z9BQRFoadSdQ7lXB-1).
5. Implementación en ASP.NET Core.
6. PostgreSQL/Supabase: esquema, conexión, consultas y evidencia de persistencia.
7. Validación del CRUD: crear, consultar, actualizar y desactivar/reactivar sin borrado físico ordinario.
8. Instrucciones para abrir y ejecutar en Visual Studio 2022.

## Cómo abrir la solución en Visual Studio 2022

1. Abre Visual Studio 2022.
2. En la pantalla inicial, selecciona **Abrir un proyecto o una solución**.
3. Busca `C:\Users\Microsoft Windows\Source\Repos\fcobustosreyes\Optica-Vision\OpticaVision.sln`.
4. Selecciona `OpticaVision.sln` y pulsa **Abrir**.
5. En el Explorador de soluciones, verifica que aparezca `OpticaVision.Web`.
6. Ejecuta con **F5** o con el botón verde **Iniciar**.

El proyecto apunta actualmente a `net10.0`. Visual Studio 2022 puede indicar que ese destino no está soportado oficialmente. No cambies el proyecto a una versión antigua sin revisar primero soporte y seguridad; si Visual Studio no compila, usa la terminal con el SDK .NET 10 o una versión de Visual Studio que soporte .NET 10.

## Estado de la aplicación que debe declararse en la defensa

El código conectado actualmente cubre inicio de sesión, perfiles/roles y CRUD de clientes/usuarios. Las pantallas de citas, ventas, compras, inventario, proveedores y reportes aún son demostrativas. No se debe afirmar que Supabase, GitHub, la publicación web o todos los módulos están terminados hasta tener evidencia de que funcionan.
