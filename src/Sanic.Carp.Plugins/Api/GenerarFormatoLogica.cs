using System;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// La lógica de <c>sanic_carp_capi_generarformato</c> (diseno/03 1.1 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Solo conoce puertos: la API (<see cref="GenerarFormatoApi"/>)
    /// decide con qué servicio construyó cada uno. Sin campos mutables ni lectura del reloj fuera de <see cref="IReloj"/>.
    /// <para>
    /// <b>Qué servicio hace qué</b> (la API arma los puertos así; las pruebas lo miden con <c>Consultas</c> y <c>Escrituras</c> del doble): LLAMADOR: <paramref name="arreglosComoLlamador"/>,
    /// <paramref name="cuotas"/>, <paramref name="clientes"/>, <paramref name="usuarios"/> y <paramref name="accesos"/>. SYSTEM: <paramref name="arreglosComoSistema"/>,
    /// <paramref name="consecutivos"/>, <paramref name="versiones"/>, <paramref name="lecturasDeVersion"/> y la configuración. Hay UN solo servicio de cada tipo por ejecución.
    /// </para>
    /// <para>
    /// <b>Contrato de <see cref="Ejecutar"/>, en orden</b> (el primer rechazo gana; ninguna escritura antes del paso 11):
    /// <list type="number">
    /// <item><c>ArregloId</c> nulo ⇒ <c>CARP-GEN-001</c>. No toca ningún puerto.</item>
    /// <item><c>ahora</c> = <c>reloj.AhoraUtc</c>, una sola lectura.</item>
    /// <item>Arreglo: <c>arreglosComoLlamador.Leer(id, ArmadorDeDatosDelFormato.ColumnasDelArreglo)</c>. <c>null</c> (no existe O sin acceso) ⇒ <c>CARP-GEN-002</c>, el mismo texto (no revelar existencia).</item>
    /// <item>Cuotas: <c>cuotas.Listar(id, ColumnasDeLaCuota)</c> (ya ordenadas por <c>sanic_numero</c>). Deben ser exactamente <c>sanic_plazo</c> y numeradas 1..plazo; si no ⇒ <c>CARP-GEN-005</c> con {n} = cuántas
    /// hay y {plazo}. Un <c>sanic_plazo</c> ausente cuenta como 0.</item>
    /// <item>Cliente: congelado = <c>sanic_fechacongelamiento</c> presente. Si NO está congelado se lee <c>clientes.Leer(sanic_clienteid, ColumnasDelCliente)</c> con el llamador; un Cliente que no se puede leer (o un arreglo sin
    /// <c>sanic_clienteid</c>) ⇒ <c>CARP-GEN-002</c> (el diseño no lo dice; decisión del arquitecto: no se congela ni se imprime en blanco una instantánea que no se pudo leer). Congelado: no se lee el Cliente (PA-GEN-03).
    /// <c>oficial</c> = <c>usuarios.NombreCompleto(sanic_oficialid)</c> (llamador); ilegible o ausente ⇒ <c>null</c>, no es error.</item>
    /// <item>Versión a usar: SIN <c>VersionPreviaId</c>, la <c>sanic_versionplantillaid</c> del arreglo (cualquier estado: una Histórica es válida para un arreglo antiguo); un arreglo sin versión ⇒ <c>CARP-GEN-003</c>.
    /// CON <c>VersionPreviaId</c> (vista previa), en este orden: (a) <c>versiones.Leer(previa, [sanic_estado, sanic_huella, sanic_capacidad])</c> con SYSTEM; <c>null</c> ⇒ <c>CARP-GEN-007</c> (no existe y sin permiso dan el mismo
    /// mensaje); (b) <c>sanic_estado</c> distinto de Borrador ⇒ <c>CARP-GEN-006</c>; (c) <c>accesos.PuedeEscribir(sanic_carp_tbl_versionplantilla, previa, ActorId)</c> falso ⇒ <c>CARP-GEN-007</c>.</item>
    /// <item>Con SYSTEM: <c>versiones.Leer(version, [sanic_estado, sanic_huella, sanic_capacidad])</c> (si no se leyó en 6a; <c>null</c> ⇒ <c>CARP-GEN-003</c>); <c>lecturasDeVersion.ArchivoDeLaVersion(version)</c> (<c>null</c> ⇒ <c>CARP-GEN-003</c>);
    /// <c>lecturasDeVersion.FilasDeMapeoActivas(version)</c>.</item>
    /// <item><c>sanic_plazo</c> mayor que <c>sanic_capacidad</c> ⇒ <c>CARP-GEN-004</c> con {plazo} y {capacidad}.</item>
    /// <item>Número: Borrador con <c>sanic_numeroprovisional</c> guardado ⇒ ese; Borrador sin él ⇒ <c>ConsecutivoDeCredito.Siguiente(consecutivos.NumerosDelCredito(producto, numeroproducto))</c> (SYSTEM: cuenta TODOS los arreglos del
    /// crédito, también los que el llamador no lee; un siguiente mayor que 9999 lanza <c>CARP-ARR-023</c>); el resto de los estados ⇒ <c>sanic_numeroarreglo</c> (puede ser <c>null</c>). Datos: <see cref="ArmadorDeDatosDelFormato.Armar"/>.
    /// Huella esperada: la <c>sanic_huella</c> de la versión (ausente ⇒ cadena vacía, que no coincide y da <c>CARP-GEN-008</c>); en VISTA PREVIA, <c>generador.CalcularHuella(archivo)</c> (el borrador puede no tener huella).</item>
    /// <item><c>generador.Generar(archivo, huella, mapeo, datos)</c> con el mapeo convertido por <c>ClavesDelGenerador.DeFila</c>. El ÚNICO <c>catch</c> de esta clase: <c>HuellaNoCoincideException</c> ⇒ <c>CARP-GEN-008</c>;
    /// <c>PlantillaInvalidaException</c> ⇒ <c>-009</c>; <c>MapeoInvalidoException</c> ⇒ <c>-010</c> con {detalle} = sus errores unidos por <c>"; "</c>; <c>DatosInvalidosException</c> ⇒ <c>-011</c> con {detalle} = el mensaje. Ninguna lleva
    /// valores del cliente. Se traza el TIPO y nunca el mensaje de la causa (el adaptador ya trazó la causa interna). Es seguro atraparlas acá porque todavía no hubo ninguna escritura (0.5). Cualquier otra excepción sube sin atraparse.</item>
    /// <item>Si NO es vista previa, escribe con SYSTEM en este orden, sin atrapar nada: (11.0) <c>configuracion.TeamPropietarioId</c> (una lectura; un <c>CARP-CFG-*</c> sube sin haber escrito nada); (a) si el arreglo está ACTIVO
    /// (<c>statecode</c> 0) y no congelado: <c>arreglosComoSistema.Actualizar(id, …)</c> con <c>sanic_fechacongelamiento = ahora</c>, las cuatro <c>sanic_instantanea*</c> copiadas del Cliente (un dato <c>null</c> se escribe como
    /// <c>null</c>) y, SOLO si es Borrador, <c>sanic_numeroprovisional</c> = el número del paso 9; un arreglo inactivo (Terminado, Anulado, Descartado) o ya congelado NO se escribe (OA-9, PA-GEN-03, PA-GEN-22); (b)
    /// <c>arreglosComoSistema.RegistrarGeneracion(id, version, ActorId, ahora, team)</c>: la bitácora se escribe SIEMPRE (también con el arreglo inactivo y también si ya estaba congelado). El actor sale de <c>Pedido.ActorId</c>, nunca de la identidad del servicio.</item>
    /// <item>Resultado: <c>Archivo</c> = Base64 del libro; <c>NombreDelArchivo</c> = <c>"F-NIC-0000052-" + sanic_nombre + ".xlsx"</c>; <c>NumeroProvisional</c> = el número del paso 9 si es Borrador, 0 si no; <c>Congelado</c> = estaba congelado o
    /// esta llamada lo congeló (en vista previa, el estado que ya tenía); <c>Advertencias</c> = las del generador unidas por <c>"\n"</c> (vacío si no hay).</item>
    /// </list>
    /// Toda la llamada es UNA transacción: el archivo se genera antes de escribir y una falla de la plataforma (<c>FaultException</c>) NO se atrapa acá: la base la vuelve <c>CARP-GEN-900</c> y la transacción revierte (PA-GEN-23). La
    /// vista previa no escribe nada, ni siquiera lee la configuración (DC-18). Traza solo ids y códigos.
    /// </para>
    /// </summary>
    internal sealed class GenerarFormatoLogica
    {
        /// <summary>Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/> (<paramref name="trazar"/> puede ser <c>null</c>). No consulta nada al construirse.</summary>
        public GenerarFormatoLogica(IArreglos arreglosComoLlamador, IArreglos arreglosComoSistema, ICuotas cuotas, IClientes clientes, IUsuarios usuarios, IConsecutivos consecutivos,
            IVersiones versiones, ILecturasDeVersion lecturasDeVersion, IAccesosDeFila accesos, IGeneradorDeFormato generador, IConfiguracionDelArea configuracion, IReloj reloj,
            Action<string> trazar)
        {
            throw new NotImplementedException();
        }

        /// <summary>Contrato en la descripción de la clase. Rechaza lanzando <see cref="Dominio.Errores.ErrorDeNegocio"/>; <paramref name="pedido"/> nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public ResultadoDeGenerarFormato Ejecutar(PedidoDeGenerarFormato pedido)
        {
            throw new NotImplementedException();
        }
    }
}
