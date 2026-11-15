begin;
alter table optica.compras add column if not exists numero_poliza varchar(80);
create unique index if not exists compras_numero_poliza_unico
on optica.compras(lower(btrim(numero_poliza))) where nullif(btrim(numero_poliza),'') is not null;
commit;
