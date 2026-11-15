namespace OpticaVision.Web.Models;

public record CampoGestion(string Columna, string Etiqueta, string Tipo = "text", bool Obligatorio = true,
    int Maximo = 150, string? OpcionesSql = null);
public record RecursoGestion(string Tabla, string Titulo, string Modulo, CampoGestion[] Campos,
    string? Actor = null, string? Padre = null, string? ClavePadre = null)
{
    public bool EsDocumento => Actor is not null;
    public bool EsCatalogo => !EsDocumento && Padre is null;
}

public static class RecursosGestion
{
    private static CampoGestion Selector(string col, string titulo, string sql, bool obligatorio = true)
        => new(col, titulo, "bigint", obligatorio, OpcionesSql: sql);

    private const string Productos = "select id,codigo || ' · ' || nombre from optica.productos where activo order by nombre";
    private const string Bodegas = "select id,nombre from optica.bodegas where activo order by nombre";
    public static readonly IReadOnlyDictionary<string, RecursoGestion> Todos = new RecursoGestion[]
    {
        new("oftalmologos","Oftalmólogos","servicios",[
            new("codigo","Código",Maximo:30),new("nombre","Nombre completo","nombre"),new("telefono","Teléfono","tel",false,30)]),
        new("categorias_servicio","Categorías de servicios","servicios",[new("nombre","Categoría","nombre",Maximo:100)]),
        new("servicios","Catálogo de servicios","servicios",[
            new("codigo","Código",Maximo:30),new("nombre","Servicio","nombre",Maximo:120),
            Selector("categoria_id","Categoría","select id,nombre from optica.categorias_servicio where activo order by nombre"),
            new("precio","Precio (C$)","numeric"),new("descripcion","Descripción",Obligatorio:false,Maximo:500)]),
        new("atenciones_servicio","Atenciones y exámenes","servicios",[
            Selector("cliente_id","Cliente","select id,nombres || ' ' || apellidos from optica.clientes where activo order by apellidos,nombres"),
            Selector("servicio_id","Servicio","select id,nombre from optica.servicios where activo order by nombre"),
            new("precio","Monto del servicio (C$)","numeric"),
            new("metodo","Método de pago","metodo"),
            new("referencia","Referencia del pago",Obligatorio:false)]),
        new("proveedores","Proveedores","proveedores", [
            new("nombre","Nombre comercial","nombre"), new("identificacion_fiscal","Identificación fiscal",Obligatorio:false,Maximo:40),
            new("contacto","Contacto",Obligatorio:false), new("telefono","Teléfono","tel",false,30),
            new("correo","Correo","email",false,254), new("direccion","Dirección",Obligatorio:false,Maximo:500)]),
        new("productos","Productos","inventario", [
            new("codigo","Código de producto",Maximo:50), new("nombre","Nombre del producto","nombre"),
            Selector("categoria_id","Categoría","select id,nombre from optica.categorias where activo order by nombre"),
            Selector("marca_id","Marca","select id,nombre from optica.marcas where activo order by nombre",false),
            new("modelo","Modelo","nombre",false,80), new("color","Color",Obligatorio:false,Maximo:50),
            new("precio_venta","Precio de venta (C$)","numeric"),new("stock_minimo","Existencia mínima","int")]),
        new("categorias","Categorías","inventario",[new("nombre","Nombre","nombre",Maximo:80)]),
        new("marcas","Marcas","inventario",[new("nombre","Nombre","nombre",Maximo:80)]),
        new("bodegas","Bodegas","inventario",[new("nombre","Nombre","nombre",Maximo:80),new("ubicacion","Ubicación",Obligatorio:false)]),
        new("ordenes_compra","Órdenes de compra","compras",[
            new("numero","Número de orden","autonumero",false),
            Selector("proveedor_id","Proveedor","select id,nombre from optica.proveedores where activo order by nombre"),
            new("observaciones","Observaciones",Obligatorio:false,Maximo:500)],"creado_por"),
        new("detalle_ordenes_compra","Productos de la orden","compras",[
            Selector("producto_id","Producto",Productos),new("cantidad","Cantidad","positivo"),new("costo_unitario","Costo unitario (C$)","numeric")],Padre:"ordenes_compra",ClavePadre:"orden_id"),
        new("compras","Compras","compras",[
            Selector("proveedor_id","Proveedor","select id,nombre from optica.proveedores where activo order by nombre"),
            new("numero_documento_proveedor","Factura del proveedor",Maximo:80),
            new("numero_poliza","Número de póliza",Maximo:80),
            new("observaciones","Observaciones",Obligatorio:false,Maximo:500)],"creado_por"),
        new("ventas","Ventas","ventas",[
            Selector("cliente_id","Cliente","select id,nombres || ' ' || apellidos from optica.clientes where activo order by apellidos,nombres",false),
            Selector("bodega_id","Bodega",Bodegas)],"vendedor_id"),
        new("recepciones","Recepciones","recepciones",[
            Selector("compra_id","Compra y documento","select id,concat('#',id,' · ',numero_documento_proveedor) from optica.compras where estado='confirmada' order by id desc"),
            Selector("bodega_id","Bodega",Bodegas),new("observaciones","Observaciones",Obligatorio:false,Maximo:500)],"recibido_por"),
        new("conteos_inventario","Conteos de inventario","inventario",[
            Selector("bodega_id","Bodega",Bodegas),new("observaciones","Motivo del conteo",Maximo:500)],"creado_por"),
        new("detalle_conteos","Líneas del conteo","inventario",[
            Selector("producto_id","Producto",Productos),new("cantidad_sistema","Unidades al inicio","int"),
            new("cantidad_contada","Unidades contadas","int"),new("motivo","Motivo de la diferencia",Obligatorio:false,Maximo:500)],Padre:"conteos_inventario",ClavePadre:"conteo_id"),
        new("detalle_compras","Productos de la compra","compras",[
            Selector("producto_id","Producto",Productos), new("cantidad","Cantidad","positivo"),new("costo_unitario","Costo unitario (C$)","numeric")],Padre:"compras",ClavePadre:"compra_id"),
        new("detalle_ventas","Productos de la venta","ventas",[
            Selector("producto_id","Producto",Productos),new("cantidad","Cantidad","positivo"),new("precio_unitario","Precio unitario (C$)","numeric"),new("descuento_unitario","Descuento por unidad (C$)","numeric")],Padre:"ventas",ClavePadre:"venta_id"),
        new("pagos_venta","Pagos","ventas",[
            new("metodo","Método de pago","metodo"),new("monto","Importe (C$)","importe"),
            new("referencia","Referencia",Obligatorio:false)],Padre:"ventas",ClavePadre:"venta_id"),
        new("detalle_recepciones","Productos recibidos","recepciones",[
            Selector("detalle_compra_id","Producto de la compra","select d.id,p.codigo || ' · ' || p.nombre from optica.detalle_compras d join optica.productos p on p.id=d.producto_id join optica.recepciones r on r.compra_id=d.compra_id where r.id=@padre order by p.nombre"),
            new("cantidad_recibida","Cantidad recibida","positivo")],Padre:"recepciones",ClavePadre:"recepcion_id")
    }.ToDictionary(r => r.Tabla);
}

public record RegistroGestion(long Id, string Estado, string[] Valores);
public sealed class GestionPagina(RecursoGestion recurso, long? padre)
{
    public RecursoGestion Recurso { get; } = recurso;
    public long? Padre { get; } = padre;
    public long? Id { get; set; }
    public bool Editable { get; set; } = true;
    public Dictionary<string,string> Valores { get; set; } = [];
    public Dictionary<string,Dictionary<string,string>> Opciones { get; } = [];
    public Dictionary<string,Dictionary<string,string>> Etiquetas { get; } = [];
    public List<RegistroGestion> Filas { get; } = [];
}








