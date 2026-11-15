begin;
create table if not exists optica.operaciones_inventario(
 id bigint generated always as identity primary key,
 tipo text not null check(tipo in ('dano','traslado')),
 producto_id bigint not null references optica.productos,
 origen_id bigint not null references optica.bodegas,
 destino_id bigint references optica.bodegas,
 cantidad integer not null check(cantidad>0),motivo text not null check(length(btrim(motivo))>=5),
 responsable_id uuid not null references optica.perfiles,
 conteo_salida_id bigint not null references optica.conteos_inventario,
 conteo_entrada_id bigint references optica.conteos_inventario,
 fecha timestamptz not null default now(),
 check((tipo='dano' and destino_id is null) or (tipo='traslado' and destino_id is not null and destino_id<>origen_id)));
alter table optica.operaciones_inventario enable row level security;
create or replace function optica.registrar_operacion_inventario(
 p_tipo text,p_producto bigint,p_origen bigint,p_destino bigint,p_cantidad integer,p_motivo text,p_actor uuid)
returns bigint language plpgsql set search_path=optica,pg_catalog as $$
declare salida bigint; entrada bigint; saldo bigint; resultado bigint;
begin
 if p_tipo not in ('dano','traslado') or p_tipo is null or p_cantidad is null or p_cantidad<=0 or length(btrim(coalesce(p_motivo,'')))<5 then raise exception 'Revisa el tipo, cantidad y motivo'; end if;
 if p_tipo='dano' then p_destino:=null; end if;
 if p_tipo='traslado' and (p_destino is null or p_destino=p_origen) then raise exception 'Selecciona una bodega de destino diferente'; end if;
 perform id from bodegas where id in (p_origen,p_destino) order by id for update;
 if not exists(select 1 from bodegas where id=p_origen and activo) or (p_tipo='traslado' and not exists(select 1 from bodegas where id=p_destino and activo)) then raise exception 'Selecciona bodegas activas'; end if;
 if not exists(select 1 from productos where id=p_producto and activo) then raise exception 'Selecciona un producto activo'; end if;
 if exists(select 1 from conteos_inventario where bodega_id in(p_origen,p_destino) and estado='abierto') then raise exception 'Finaliza los conteos abiertos antes de registrar movimientos'; end if;
 select coalesce((select cantidad from existencias where bodega_id=p_origen and producto_id=p_producto),0) into saldo;
 if saldo<p_cantidad then raise exception 'Stock insuficiente'; end if;
 salida:=iniciar_conteo(p_origen,p_actor,p_motivo);
 update detalle_conteos set cantidad_contada=cantidad_sistema, motivo=p_motivo where conteo_id=salida;
 update detalle_conteos set cantidad_contada=cantidad_sistema-p_cantidad where conteo_id=salida and producto_id=p_producto;
 update conteos_inventario set estado='aprobado',aprobado_por=p_actor where id=salida;
 if p_tipo='traslado' then
 entrada:=iniciar_conteo(p_destino,p_actor,p_motivo);
 update detalle_conteos set cantidad_contada=cantidad_sistema,motivo=p_motivo where conteo_id=entrada;
 update detalle_conteos set cantidad_contada=cantidad_sistema+p_cantidad where conteo_id=entrada and producto_id=p_producto;
 update conteos_inventario set estado='aprobado',aprobado_por=p_actor where id=entrada;
 end if;
 insert into operaciones_inventario(tipo,producto_id,origen_id,destino_id,cantidad,motivo,responsable_id,conteo_salida_id,conteo_entrada_id)
 values(p_tipo,p_producto,p_origen,p_destino,p_cantidad,p_motivo,p_actor,salida,entrada) returning id into resultado;
 return resultado;
end $$;
commit;
