begin;
create table if not exists optica.oftalmologos(
 id bigint generated always as identity primary key,
 codigo varchar(30) not null unique,
 nombre varchar(150) not null,
 telefono varchar(30),activo boolean not null default true);
alter table optica.oftalmologos enable row level security;
alter table optica.citas add column if not exists oftalmologo_id bigint references optica.oftalmologos(id);
create index if not exists citas_oftalmologo_fecha on optica.citas(oftalmologo_id,fecha);
commit;
