using System;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Tabla "opción de <see cref="CampoMapeable"/> ⇒ clave del generador" y tabla de textos formales de Tipo y Moneda
    /// (diseno/03 1.1 "Datos del generador"; OA-7, decidida el 2026-10-10). Es el único lugar donde viven las dos, aparte de las
    /// etiquetas sin tildes del choice (BP-PP-197).
    /// <para>
    /// Tabla de claves (1.1): FechaDeFormalizacion = <c>fechaFormalizacion</c>, OficialDeArreglo = <c>oficial</c>, NombreDelCliente =
    /// <c>clienteNombre</c>, CedulaDelCliente = <c>clienteCedula</c>, CorreoDelCliente = <c>clienteCorreo</c>, CelularDelCliente =
    /// <c>clienteCelular</c>, NumeroDePrestamoOTarjeta = <c>numeroProducto</c>, TipoDeArreglo = <c>tipo</c>, NumeroDeArreglo =
    /// <c>numeroArreglo</c>, SaldoInicial = <c>saldoInicial</c>, MontoAbonado = <c>montoAbonado</c>, SaldoRemanente =
    /// <c>saldoRemanente</c>, PlazoEnCuotas = <c>plazo</c>, CuotaMensual = <c>cuota</c>, TotalAPagar = <c>totalPagar</c>,
    /// FechaDeInicio = <c>fechaInicio</c>, FechaDeFinalizacion = <c>fechaFin</c>, CuentaADebitar = <c>cuentaDebitar</c>, Comentarios =
    /// <c>comentarios</c>, Moneda = <c>moneda</c>; las cinco columnas de cuota y los dos totales usan las constantes de <see cref="Campos"/>.
    /// </para>
    /// Toda comparación es ordinal y no depende de la cultura del hilo (diseno/03 0.6).
    /// </summary>
    internal static class ClavesDelGenerador
    {
        /// <summary>Clave del generador de una opción definida del choice; <see cref="ArgumentOutOfRangeException"/> si la opción no está definida.</summary>
        public static string Clave(CampoMapeable campo)
        {
            throw new NotImplementedException("Lote 2: tabla opción ⇒ clave de diseno/03 1.1.");
        }

        /// <summary>Como <see cref="Clave"/>, pero devuelve <c>false</c> (y <c>clave = null</c>) para una opción que el código aún no conoce.</summary>
        public static bool TryClave(CampoMapeable campo, out string clave)
        {
            throw new NotImplementedException("Lote 2: tabla opción ⇒ clave de diseno/03 1.1.");
        }

        /// <summary>ValorSimple ⇒ Simple, TotalCalculado ⇒ Total, ColumnaDeBloqueDeCuotas ⇒ Bloque (4.3); <see cref="ArgumentOutOfRangeException"/> si no está definido.</summary>
        public static TipoMapeo TipoDelGenerador(TipoDeMapeo tipo)
        {
            throw new NotImplementedException("Lote 2: TipoDeMapeo ⇒ TipoMapeo de diseno/03 4.3.");
        }

        /// <summary>Texto formal del tipo de arreglo para el documento firmado: "Temporal" / "Cancelación" (OA-7).</summary>
        public static string TextoFormal(TipoDeArreglo tipo)
        {
            throw new NotImplementedException("Lote 2: textos formales de OA-7.");
        }

        /// <summary>Texto formal de la moneda: "Córdobas" / "Dólares" (OA-7).</summary>
        public static string TextoFormal(Moneda moneda)
        {
            throw new NotImplementedException("Lote 2: textos formales de OA-7.");
        }

        /// <summary>
        /// Convierte una fila del mapeo del dominio en la del generador: la opción pasa a clave, el tipo a <see cref="TipoMapeo"/> y las
        /// filas máximas nulas a 0; hoja y celda pasan tal cual. Una opción o un tipo que el código no conoce lanza
        /// <see cref="MapeoInvalidoException"/> con un error que nombra la celda y el valor numérico (la Custom API la traduce a
        /// <c>CARP-GEN-010</c>; la publicación la frena antes con <c>CARP-VER-007</c>). Una fila nula lanza <see cref="ArgumentNullException"/>.
        /// </summary>
        public static MapeoCelda DeFila(FilaDeMapeo fila)
        {
            throw new NotImplementedException("Lote 2: conversión FilaDeMapeo ⇒ MapeoCelda (diseno/03 4.3).");
        }
    }
}
