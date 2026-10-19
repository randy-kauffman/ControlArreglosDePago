using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
        // Tabla literal de diseno/03 1.1. Las cinco columnas de cuota y los dos totales usan las constantes de Campos, de modo que
        // el generador y esta tabla no pueden separarse en un renombre (la prueba de cobertura compara contra la tabla del diseño).
        // De solo lectura: estado estático sin mutación posible (guardia PA5E_GRD_01, cierre del 5e).
        private static readonly IReadOnlyDictionary<CampoMapeable, string> Claves = new ReadOnlyDictionary<CampoMapeable, string>(new Dictionary<CampoMapeable, string>
        {
            { CampoMapeable.FechaDeFormalizacion, "fechaFormalizacion" },
            { CampoMapeable.OficialDeArreglo, "oficial" },
            { CampoMapeable.NombreDelCliente, "clienteNombre" },
            { CampoMapeable.CedulaDelCliente, "clienteCedula" },
            { CampoMapeable.CorreoDelCliente, "clienteCorreo" },
            { CampoMapeable.CelularDelCliente, "clienteCelular" },
            { CampoMapeable.NumeroDePrestamoOTarjeta, "numeroProducto" },
            { CampoMapeable.TipoDeArreglo, "tipo" },
            { CampoMapeable.NumeroDeArreglo, "numeroArreglo" },
            { CampoMapeable.SaldoInicial, "saldoInicial" },
            { CampoMapeable.MontoAbonado, "montoAbonado" },
            { CampoMapeable.SaldoRemanente, "saldoRemanente" },
            { CampoMapeable.PlazoEnCuotas, "plazo" },
            { CampoMapeable.CuotaMensual, "cuota" },
            { CampoMapeable.TotalAPagar, "totalPagar" },
            { CampoMapeable.FechaDeInicio, "fechaInicio" },
            { CampoMapeable.FechaDeFinalizacion, "fechaFin" },
            { CampoMapeable.CuentaADebitar, "cuentaDebitar" },
            { CampoMapeable.Comentarios, "comentarios" },
            { CampoMapeable.CuotaNumero, Campos.CuotaNumero },
            { CampoMapeable.CuotaFechaDeVencimiento, Campos.CuotaFecha },
            { CampoMapeable.CuotaMonto, Campos.CuotaMonto },
            { CampoMapeable.CuotaFechaDePago, Campos.CuotaFechaPago },
            { CampoMapeable.CuotaMontoPagado, Campos.CuotaMontoPagado },
            { CampoMapeable.TotalMontosDeCuota, Campos.TotalMontoCuotas },
            { CampoMapeable.TotalMontosPagados, Campos.TotalMontoPagado },
            { CampoMapeable.Moneda, "moneda" }
        });

        /// <summary>Clave del generador de una opción definida del choice; <see cref="ArgumentOutOfRangeException"/> si la opción no está definida.</summary>
        public static string Clave(CampoMapeable campo)
        {
            string clave;
            if (!TryClave(campo, out clave))
            {
                throw new ArgumentOutOfRangeException(nameof(campo), (int)campo, "La opción " + Num((int)campo) + " de CampoMapeable no tiene clave del generador.");
            }
            return clave;
        }

        /// <summary>Como <see cref="Clave"/>, pero devuelve <c>false</c> (y <c>clave = null</c>) para una opción que el código aún no conoce.</summary>
        public static bool TryClave(CampoMapeable campo, out string clave)
        {
            return Claves.TryGetValue(campo, out clave);
        }

        /// <summary>ValorSimple ⇒ Simple, TotalCalculado ⇒ Total, ColumnaDeBloqueDeCuotas ⇒ Bloque (4.3); <see cref="ArgumentOutOfRangeException"/> si no está definido.</summary>
        public static TipoMapeo TipoDelGenerador(TipoDeMapeo tipo)
        {
            TipoMapeo traducido;
            if (!TryTipoDelGenerador(tipo, out traducido))
            {
                throw new ArgumentOutOfRangeException(nameof(tipo), (int)tipo, "El tipo de mapeo " + Num((int)tipo) + " no tiene traducción al generador.");
            }
            return traducido;
        }

        /// <summary>Texto formal del tipo de arreglo para el documento firmado: "Temporal" / "Cancelación" (OA-7).</summary>
        public static string TextoFormal(TipoDeArreglo tipo)
        {
            // Las etiquetas del choice van sin tildes (BP-PP-197) y eso no se imprime en un documento firmado (OA-7, opción A).
            switch (tipo)
            {
                case TipoDeArreglo.Temporal: return "Temporal";
                case TipoDeArreglo.Cancelacion: return "Cancelación";
                default:
                    throw new ArgumentOutOfRangeException(nameof(tipo), (int)tipo, "El tipo de arreglo " + Num((int)tipo) + " no tiene texto formal.");
            }
        }

        /// <summary>Texto formal de la moneda: "Córdobas" / "Dólares" (OA-7).</summary>
        public static string TextoFormal(Moneda moneda)
        {
            switch (moneda)
            {
                case Moneda.Cordobas: return "Córdobas";
                case Moneda.Dolares: return "Dólares";
                default:
                    throw new ArgumentOutOfRangeException(nameof(moneda), (int)moneda, "La moneda " + Num((int)moneda) + " no tiene texto formal.");
            }
        }

        /// <summary>
        /// Convierte una fila del mapeo del dominio en la del generador: la opción pasa a clave, el tipo a <see cref="TipoMapeo"/> y las
        /// filas máximas nulas a 0; hoja y celda pasan tal cual. Una opción o un tipo que el código no conoce lanza
        /// <see cref="MapeoInvalidoException"/> con un error que nombra la celda y el valor numérico (la Custom API la traduce a
        /// <c>CARP-GEN-010</c>; la publicación la frena antes con <c>CARP-VER-007</c>). Una fila nula lanza <see cref="ArgumentNullException"/>.
        /// </summary>
        public static MapeoCelda DeFila(FilaDeMapeo fila)
        {
            if (fila == null) throw new ArgumentNullException(nameof(fila));

            // Se reúnen los dos problemas posibles de la fila (igual que el resto del generador: todos los errores, no solo el primero).
            var errores = new List<string>();
            var donde = "La celda '" + fila.Celda + "' de la hoja '" + fila.Hoja + "'";

            string clave;
            if (!TryClave(fila.Campo, out clave))
            {
                errores.Add(donde + " usa el campo " + Num((int)fila.Campo) + ", que no es una opción conocida del mapeo.");
            }

            TipoMapeo tipo;
            if (!TryTipoDelGenerador(fila.Tipo, out tipo))
            {
                errores.Add(donde + " usa el tipo de mapeo " + Num((int)fila.Tipo) + ", que no es un tipo conocido.");
            }

            if (errores.Count > 0) throw new MapeoInvalidoException(errores);
            return new MapeoCelda(fila.Hoja, fila.Celda, tipo, clave, fila.FilasMaximas ?? 0);
        }

        private static bool TryTipoDelGenerador(TipoDeMapeo tipo, out TipoMapeo traducido)
        {
            switch (tipo)
            {
                case TipoDeMapeo.ValorSimple: traducido = TipoMapeo.Simple; return true;
                case TipoDeMapeo.TotalCalculado: traducido = TipoMapeo.Total; return true;
                case TipoDeMapeo.ColumnaDeBloqueDeCuotas: traducido = TipoMapeo.Bloque; return true;
                default: traducido = default(TipoMapeo); return false;
            }
        }

        private static string Num(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
