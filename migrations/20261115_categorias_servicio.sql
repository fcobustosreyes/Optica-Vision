begin;
create table if not exists optica.categorias_servicio(
 id bigint generated always as identity primary key,
 nombre varchar(100) not null unique,
 activo boolean not null default true);
alter table optica.categorias_servicio enable row level security;
alter table optica.servicios add column if not exists categoria_id bigint references optica.categorias_servicio(id);
commit;
