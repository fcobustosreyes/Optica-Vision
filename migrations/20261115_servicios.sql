begin;
create table if not exists optica.servicios(
 id bigint generated always as identity primary key,
 codigo varchar(30) not null unique,nombre varchar(120) not null unique,
 precio numeric(14,2) not null check(precio>=0),descripcion text,
 activo boolean not null default true);
create table if not exists optica.atenciones_servicio(
 id bigint generated always as identity primary key,
 cliente_id bigint not null references optica.clientes,
 servicio_id bigint not null references optica.servicios,
 precio numeric(14,2) not null check(precio>=0),
 metodo varchar(20) not null check(metodo in ('efectivo','tarjeta','transferencia')),
 referencia varchar(150),activo boolean not null default true,
 fecha timestamptz not null default now());
alter table optica.servicios enable row level security;
alter table optica.atenciones_servicio enable row level security;
insert into optica.servicios(codigo,nombre,precio,descripcion,activo) values
('SRV-001','Examen visual',0,'Precio pendiente de configurar',false),
('SRV-002','Control de graduación',0,'Precio pendiente de configurar',false),
('SRV-003','Ajuste de montura',0,'Precio pendiente de configurar',false)
on conflict(codigo) do nothing;
commit;

