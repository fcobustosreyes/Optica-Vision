# Optica Vision

Sistema académico para la gestión de una óptica: agenda, clientes, catálogo, compras, inventario, ventas internas y reportes. Backend ASP.NET Core MVC, PostgreSQL en Supabase y control de acceso por roles.

## Estado actual

- Implementados en el código: autenticación con Supabase Auth, perfiles y roles, creación inicial del superadministrador, administración de cuentas con desactivación/reactivación, y CRUD de clientes con auditoría.
- La app accede a PostgreSQL desde el servidor ASP.NET Core con Npgsql. Las claves no van en el navegador ni en archivos versionados.
- Las vistas de citas, ventas, compras, inventario, proveedores y reportes aún son demostrativas; no registran operaciones reales.
- El esquema incremental para el proyecto Supabase existente está en `docs/Preparar_Supabase_Existente.sql`. Todavía no se ha aplicado al proyecto remoto.
- El sitio todavía no está publicado. No se deben registrar datos reales hasta configurar las credenciales, aplicar el esquema y completar la revisión de seguridad.

## Requisitos

- .NET 10 SDK y runtime.
- Npgsql 10.0.3 (se restaura desde NuGet).
- Acceso al proyecto Supabase `amyjnztkqlxobpbreslj`.
- Para abrir el destino `net10.0` desde Visual Studio, se requiere Visual Studio 2026 18.0 o posterior. Visual Studio 2022 no admite oficialmente ese destino; se puede ejecutar desde terminal con .NET 10 o abrir en VS Code.

## Preparar el entorno

1. Haz un respaldo de Supabase y revisa `docs/Preparar_Supabase_Existente.sql`. En el SQL Editor del proyecto existente, aplica ese script una sola vez. **No ejecutes `optica_vision.sql` en ese proyecto**, porque es el esquema de instalación inicial.
2. En Supabase, copia la cadena de conexión PostgreSQL de **Connect** (preferiblemente Session pooler para el backend) y los valores de Auth: URL del proyecto, publishable key y secret key. La secret key solo se utiliza en el servidor para crear cuentas.
3. Guarda los valores en User Secrets del proyecto web, fuera del repositorio:

```json
{
  "ConnectionStrings": {
    "OpticaPostgres": "<cadena de conexión de Supabase>"
  },
  "Supabase": {
    "Url": "https://amyjnztkqlxobpbreslj.supabase.co",
    "PublishableKey": "<publishable key>",
    "SecretKey": "<secret key; solo servidor>"
  },
  "Bootstrap": {
    "Token": "<código temporal largo y aleatorio>"
  }
}
```

4. Con Internet disponible, restaura paquetes e inicia el proyecto:

```powershell
dotnet restore OpticaVision.sln
dotnet run --project src/OpticaVision.Web
```

5. Abre `/configuracion-inicial`, crea el superadministrador y elimina `Bootstrap:Token` de User Secrets. Inicia sesión y crea las cuentas de administrador, ventas, recepción y cliente.

La conexión PostgreSQL fuerza TLS con validación del certificado. Nunca compartas las claves en GitHub, capturas, el navegador ni este chat.

## Documentación

- [Manual del proyecto](docs/Manual_Tecnico_Optica_Vision.md)
- [Lista de entrega y nota del examen](docs/Checklist_Examen.md)
- [Análisis, requisitos y base legal de Nicaragua](docs/Analisis_y_requisitos_Optica_Vision.md)
- [Guía del esquema de base de datos](LEEME_optica.md)
- [Instalación inicial PostgreSQL](optica_vision.sql)
- [Preparación incremental de Supabase existente](docs/Preparar_Supabase_Existente.sql)
- [Prototipo de Figma](https://www.figma.com/proto/BUID7VzwJXygprW9ZIiKhs/Optica-Vision?node-id=2-2&page-id=0%3A1&starting-point-node-id=2%3A2&t=Z9BQRFoadSdQ7lXB-1)

## GitHub y publicación

Repositorio compartido: <https://github.com/fcobustosreyes/Optica-Vision>. Revisa `git status` antes de confirmar. No subas User Secrets ni contraseñas. Para publicar el sitio se necesita elegir un hosting compatible con ASP.NET Core 10, configurar secretos de producción y ejecutar primero los controles de seguridad y respaldo.
