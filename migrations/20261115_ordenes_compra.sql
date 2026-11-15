begin;
create sequence if not exists optica.numero_orden_compra;
create table if not exists optica.ordenes_compra(
 id bigint generated always as identity primary key,
 numero text not null unique default ('OC-'||to_char(current_date,'YYYY')||'-'||lpad(nextval('optica.numero_orden_compra')::text,6,'0')),
 proveedor_id bigint not null references optica.proveedores,
 fecha timestamptz not null default now(),
 estado varchar(20) not null default 'borrador' check(estado in ('borrador','confirmada','anulada')),
 creado_por uuid not null references optica.perfiles,observaciones text);
create table if not exists optica.detalle_ordenes_compra(
 id bigint generated always as identity primary key,
 orden_id bigint not null references optica.ordenes_compra,
 producto_id bigint not null references optica.productos,
 cantidad integer not null check(cantidad>0),
 costo_unitario numeric(14,2) not null check(costo_unitario>=0),
 subtotal numeric(24,2) generated always as(cantidad*costo_unitario) stored,
 unique(orden_id,producto_id));
alter table optica.ordenes_compra enable row level security;
alter table optica.detalle_ordenes_compra enable row level security;
create or replace function optica.proteger_orden_compra() returns trigger language plpgsql set search_path=optica,pg_catalog as $$
begin
 if tg_op='DELETE' then raise exception 'Conserva la orden mediante anulación';end if;
 if tg_op='INSERT' then
  if new.estado<>'borrador' then raise exception 'La orden debe iniciar en borrador';end if;
  return new;
 end if;
 if old.estado='anulada' then raise exception 'Orden anulada';end if;
 if new.numero<>old.numero or new.proveedor_id<>old.proveedor_id and old.estado<>'borrador' then raise exception 'No se puede modificar la orden';end if;
 if old.estado='confirmada' and (new.estado<>'anulada' or (to_jsonb(new)-'estado')<>(to_jsonb(old)-'estado')) then raise exception 'La orden confirmada solo admite anulación';end if;
 if new.estado='confirmada' and not exists(select 1 from detalle_ordenes_compra where orden_id=new.id) then raise exception 'Agrega productos antes de confirmar';end if;
 return new;
end $$;
drop trigger if exists proteger on optica.ordenes_compra;
create trigger proteger before insert or update or delete on optica.ordenes_compra for each row execute function optica.proteger_orden_compra();
drop trigger if exists proteger on optica.detalle_ordenes_compra;
create trigger proteger before insert or update or delete on optica.detalle_ordenes_compra for each row execute function optica.proteger_detalle('ordenes_compra','orden_id');
commit;
