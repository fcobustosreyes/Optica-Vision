begin;
insert into optica.roles(nombre,descripcion) values('oftalmologo','Consulta su agenda y registra la atención de sus citas')
on conflict(nombre) do update set activo=true;
alter table optica.oftalmologos add column if not exists perfil_id uuid unique references optica.perfiles(id);
create or replace function optica.vincular_oftalmologo() returns trigger language plpgsql set search_path=optica,pg_catalog as $$
begin
 if exists(select 1 from roles where id=new.rol_id and nombre='oftalmologo') then
 insert into oftalmologos(codigo,nombre,perfil_id,activo)
 values('OFT-'||new.id::text,new.nombre_completo,new.id,new.activo)
 on conflict(perfil_id) do update set nombre=excluded.nombre,activo=excluded.activo;
 end if;
 return new;
end $$;
drop trigger if exists vincular_oftalmologo on optica.perfiles;
create trigger vincular_oftalmologo after insert or update of nombre_completo,activo,rol_id on optica.perfiles
for each row execute function optica.vincular_oftalmologo();
commit;
alter table optica.oftalmologos alter column codigo type varchar(50);
